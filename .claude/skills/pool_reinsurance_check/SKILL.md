# Проверка тест-кейсов перестраховочного пула

**Команда:** `/pool-reinsurance-check`

---

## Алгоритм

### Шаг 0. Определи окружение

1. Спроси пользователя: **«Тест или Прод?»**
2. В зависимости от ответа:

| Окружение | БД логов (шаг 1) | БД офферов (шаг 2) | Фильтр по дате |
|---|---|---|---|
| **Тест** | `InsappLogTest` | `InsappCoreTest` | `AND Date > DATEADD(day, -10, GETDATE())` |
| **Прод** | `InsappLogProd` | `InsappCoreProd` | `AND Date > '[дата релиза] +03:00'` — спроси у пользователя |

3. Если выбран **Прод** — спроси дату и время релиза. Без неё нельзя запускать.
4. Подставляй нужные БД и фильтр по дате во все запросы на шагах 1–3.

> **⚠️ Прод и timezone:** На проде колонка `Date` имеет тип `DateTimeOffset`. Фильтр **обязательно** должен включать timezone `+03:00`, например `'2026-04-27 16:26:10.774 +03:00'`. Без timezone — сравнение DateTimeOffset vs DateTime даёт **0 строк без ошибки**. На тесте колонка `DateTime`, timezone не нужен.

---

### Шаг 1. Целевой поиск по известным признакам пула

Запускай запросы **параллельно**. Для каждой СК берёшь TOP 10 заявок с признаком пула.

| ТК | СК | Таблица | Фильтр пула |
|---|---|---|---|
| 1 | СОГАЗ | SogazLogs | см. Шаг 1б ниже — признак пула неизвестен, только широкий поиск |
| 2 | ИГС | IngosLogs | `Url LIKE '%agreement/create%' AND ISJSON(ResponseBody)=1 AND (JSON_VALUE(ResponseBody,'$.warnings[0].warning')='Внимание, уровень 9' OR JSON_VALUE(ResponseBody,'$.warnings[0].warning')='Внимание, уровень 10')` |
| 3 | Альфа | AlfaLogs | `Url LIKE '%osago/calculation%' AND ISJSON(ResponseBody)=1 AND JSON_VALUE(ResponseBody,'$.calculation_details.reinsurance')='true'` |
| 4 | ВСК | VskLogs | `ResponseBody LIKE '%"isReinsurance": true%'` |
| 5 | Гелиос | GeliosLogs | `(Url LIKE '%osago/createcalc%' OR Url LIKE '%gate/getInvoicePaymentUrl%') AND ISJSON(ResponseBody)=1 AND JSON_VALUE(ResponseBody,'$.value.data.result.isReinsured')='true'` |
| 6 | Ренессанс | RenessansLogs | `ResponseBody LIKE '%"isReinsurance": true%'` |
| 7 | Согласие | SoglasieLogs | `ResponseBody LIKE '%<brief>ПерестрахованиеОСАГО</brief><name>Перестрахование ОСАГО</name><result>1</result>%'` |
| 8 | Сбер | SberLogs | см. особые правила в разделе «Ловушки» |
| 9 | Абсолют | AbsolutLogs | `Url LIKE '%/create%' AND ISJSON(ResponseBody)=1 AND JSON_VALUE(ResponseBody,'$.result.data.reinsurance')='true'` |
| 10 | АстроВолга | AstroVolgaLogs | см. Шаг 1а ниже |
| 11 | Совком | SovcomLogs | `ResponseBody LIKE '%"key": "reinsurancePool"%"value": "true"%'` |
| 12 | Энергогарант | EnergogarantLogs | `Url LIKE '%api_osago/calc%' AND ISJSON(ResponseBody)=1 AND JSON_VALUE(ResponseBody,'$.is_reinsurance')='true'` |

**Шаблон запроса:**
```sql
SELECT TOP 10 ApplicationId, Date
FROM [БД логов].dbo.[Таблица]
WHERE [фильтр пула]
AND Date > [фильтр даты из шага 0]
ORDER BY Date DESC
```

Если 0 строк → пометить ТК как **НЕТ ДАННЫХ (целевой поиск)**.

---

### Шаг 1а. ТК10 АстроВолга — особый шаг

На **Тесте** — выполняй запрос напрямую через MCP (таблица небольшая, таймаута не будет).

На **Проде** — работает через MCP. **Важно:** дата фильтра должна включать timezone (`+03:00`), иначе сравнение DateTimeOffset vs DateTime даёт 0 строк. Формат: `'2026-04-27 16:26:10.774 +03:00'`.

```sql
SELECT TOP 10 al.ApplicationId, al.Date
FROM [БД логов].dbo.AstroVolgaLogs al WITH (NOLOCK)
WHERE al.Url LIKE '%/calculator/osago%'
AND ISJSON(al.ResponseBody) = 1
AND JSON_VALUE(al.ResponseBody, '$.data.transferToPool') = 'true'
AND al.Date > 'дата_релиза +03:00'
ORDER BY al.Date DESC
```

---

### Шаг 1б. ТК1 СОГАЗ — только широкий поиск

Признак пула СОГАЗа неизвестен (commission=0.1 — НЕ признак пула). Пропустить целевой поиск для СОГАЗа на шаге 1 и полагаться **только** на широкий поиск (шаг 2) по ключевым словам в SogazLogs.

Если широкий поиск (шаг 2) найдёт попадания в SogazLogs — проверить содержимое ResponseBody, чтобы убедиться что это реально пул, а не ложное срабатывание.

---

### Шаг 2. Широкий поиск — ключевые слова во ВСЕХ таблицах логов

> **Это критически важный шаг.** Целевой поиск (шаг 1) использует только известные признаки. Но СК могут добавить признак пула без уведомления — как произошло с Энергогарантом. Широкий поиск ловит такие случаи.

Для **каждой** таблицы логов (даже если на шаге 1 было НЕТ ДАННЫХ) выполни поиск по ключевым словам:

**Ключевые слова для поиска (JSON):**
- `reinsurance`
- `isReinsurance`
- `is_reinsurance`
- `reinsurancePool`
- `transferToPool`
- `isReinsured`
- `isExtraPool`

**Ключевые слова для поиска (XML/текст):**
- `ПерестрахованиеОСАГО`
- `Перестрахование ОСАГО`
- `перестрахов`
- `IsReinsurance`
- `reinsurance`

**Шаблон запроса для каждой таблицы:**
```sql
SELECT TOP 5 ApplicationId, Date, Url
FROM [БД логов].dbo.[Таблица]
WHERE (
  ResponseBody LIKE '%reinsurance%'
  OR ResponseBody LIKE '%isReinsurance%'
  OR ResponseBody LIKE '%is_reinsurance%'
  OR ResponseBody LIKE '%reinsurancePool%'
  OR ResponseBody LIKE '%transferToPool%'
  OR ResponseBody LIKE '%isReinsured%'
  OR ResponseBody LIKE '%isExtraPool%'
  OR ResponseBody LIKE '%ПерестрахованиеОСАГО%'
  OR ResponseBody LIKE '%перестрахов%'
)
AND Date > [фильтр даты из шага 0]
ORDER BY Date DESC
```

**Полный список таблиц для широкого поиска:**

| Таблица | СК |
|---|---|
| SogazLogs | СОГАЗ |
| IngosLogs | ИГС |
| AlfaLogs | Альфа |
| VskLogs | ВСК |
| GeliosLogs | Гелиос |
| RenessansLogs | Ренессанс |
| SoglasieLogs | Согласие |
| SberLogs | Сбер |
| AbsolutLogs | Абсолют |
| AstroVolgaLogs | АстроВолга |
| SovcomLogs | Совком |
| EnergogarantLogs | Энергогарант |
| TinkoffLogs | Тинькофф |
| ResoLogs | РЕСО |
| ZettaLogs | Зетта |
| MaksLogs | МАКС |
| IntouchLogs | Интач |
| UralsibLogs | Уралсиб |
| RosgosstrahLogs | Росгосстрах |
| ChulpanLogs | Чулпан |
| HakkuLogs | ХАККУ |
| OskLogs | ОСК |

Запускай параллельно по несколько таблиц. Для больших таблиц (VskLogs, RenessansLogs, SoglasieLogs) обязательно сужай диапазон при таймауте.

**Если широкий поиск нашёл заявки, а целевой (шаг 1) — нет** → это **НОВЫЙ признак пула**, который система не обрабатывает. Это баг. Пометить ТК как **FAIL (новый признак)** и включить в отчёт.

**Если широкий поиск нашёл заявки** — проверь содержимое ResponseBody, чтобы убедиться что это реально пул, а не ложное срабатывание. Частые причины ложных срабатываний:
- `isReinsurance=null` или `isReinsurance=false` — не пул
- `result=0` в XML — пул не активирован
- Слово «перестрахов» в контексте описания услуги, а не признака

---

### Шаг 3. Проверка офферов (только для ТК где нашлись заявки)

Для каждого ТК с найденными ApplicationId выполни проверку офферов:

#### Стандартные СК (все кроме Альфы и Гелиоса)
**Ожидаемый результат:** все офферы имеют `OfferStatusTypeId=3` И `OfferTypeId IN (4,6,7,14,15,16,17,18,19,20,21)`

```sql
SELECT o.ApplicationId, o.OfferStatusTypeId, o.OfferTypeId, i.Alias
FROM [БД офферов].dbo.Applications a
JOIN [БД офферов].dbo.Offers o ON a.ApplicationId = o.ApplicationId
JOIN [БД офферов].dbo.Insurers i ON i.InsurerId = o.InsurerId
WHERE i.InsurerId = '[InsurerId СК]'
AND a.ApplicationId IN ([список ApplicationId])
```

Провал: если найдена хоть одна строка с `OfferStatusTypeId != 3` ИЛИ `OfferTypeId NOT IN (4,6,7,14,15,16,17,18,19,20,21)`

#### ТК 3 (Альфа) и ТК 5 (Гелиос) — одобрение с кроссом
**Ожидаемый результат:** офферы с `OfferStatusTypeId IN (5,6)` имеют вменённый кросс

```sql
SELECT o.ApplicationId, o.OfferStatusTypeId, o.OfferTypeId, i.Alias, u.UpsaleType, u.IsOptional
FROM [БД офферов].dbo.Applications a
JOIN [БД офферов].dbo.Offers o ON a.ApplicationId = o.ApplicationId
JOIN [БД офферов].dbo.Insurers i ON i.InsurerId = o.InsurerId
LEFT JOIN [БД офферов].dbo.Upsales u ON u.OfferId = o.OfferId
WHERE i.InsurerId = '[InsurerId СК]'
AND (o.OfferStatusTypeId = 5 OR o.OfferStatusTypeId = 6)
AND a.ApplicationId IN ([список ApplicationId])
```

Провал: если найдена строка без вменённого кросса (`UpsaleType IS NULL` или `IsOptional != 0`) или `OfferTypeId NOT IN (4,6,7,14,15,16,17,18,19,20,21)`
- Альфа: `UpsaleType=4`
- Гелиос: `UpsaleType=3`

---

### Шаг 3а. Расширенный поиск для СК без данных

Если после шагов 1–3 у СК статус **НЕТ ДАННЫХ** (0 заявок с даты релиза):

1. **Расширь дату на -1 день** от указанной пользователем даты релиза. Пример: если релиз `2026-04-27 16:26:10.774 +03:00`, ищи от `2026-04-26 16:26:10.774 +03:00`.

2. Запусти **и целевой, и широкий поиск** (шаг 1 + шаг 2) с расширенной датой для каждой такой СК.

3. **Если найден признак пула**:
   - Выведи **ApplicationId** найденных заявок
   - Проверь офферы (шаг 3) и укажи результат
   - Укажи что заявка найдена за расширенный период (до релиза)

4. **Если признак всё равно не найден** — выведи итоговый статус: **«ПУЛ НЕ ПРИХОДИТ»** (за 2 дня ни одной заявки с признаком пула).

5. Для каждого попадания широкого поиска — **обязательно проверь содержимое** ResponseBody:
   - `isReinsurance: false` / `isReinsurance: null` / `isReinsurance=null` → **не пул**, ложное срабатывание
   - `isReinsurance: true` / `transferToPool=true` / `is_reinsurance=true` → **реальный пул**

---

### Шаг 4. Сформируй отчёт

```
## Проверка пула — [Окружение, дата]

### Целевой поиск (шаг 1)
| ТК | СК | Результат | Заявок | Комментарий |
|---|---|---|---|---|
| 1 | СОГАЗ | PASS/FAIL/НЕТ ДАННЫХ | N | ... |
...

### Расширенный поиск -1 день (шаг 3а)
> Для СК со статусом НЕТ ДАННЫХ за период релиза

| ТК | СК | Пул приходит? | Данные | ApplicationId | Комментарий |
|---|---|---|---|---|---|
| 1 | СОГАЗ | Нет | 0 | — | ПУЛ НЕ ПРИХОДИТ за 2 дня |
| 7 | Согласие | Да | 1 | [ApplicationId] | PASS (до релиза) |
| 4 | ВСК | Нет | 0 | — | ПУЛ НЕ ПРИХОДИТ за 2 дня |
...

### Детали по провальным ТК:
- **ТК12 Энергогарант** — найдено X заявок с is_reinsurance=true, все OfferStatusTypeId=5 (должен быть 3)
  ApplicationId примера: [id]

### Новые признаки пула (требуют разработки):
- Энергогарант: JSON `$.is_reinsurance=true` в ответе `api_osago/calc/{id}`
```

---

## InsurerId справочник

| ТК | СК | InsurerId |
|---|---|---|
| 1 | СОГАЗ | `7A8251FF-2F25-4E7B-8CF6-9C377D5A10E9` |
| 2 | ИГС | `AEB63341-4C3B-4C05-BE2D-F0BC5A4B5E71` |
| 3 | Альфа | `CDA9423C-A34D-4752-87D3-47DEFEC5CB56` |
| 4 | ВСК | `36583F16-235C-42D6-8FCE-B04AA6459730` |
| 5 | Гелиос | `ADBD4CE5-8177-4FB0-AA9A-A22E6A9E884F` |
| 6 | Ренессанс | `DD4408A7-4898-44D9-95B3-EA7EC3BAEFA7` |
| 7 | Согласие | `FBCFD2D2-6722-4348-A1B3-B94D51491CBF` |
| 8 | Сбер | `7AB8ABDC-C8B1-49BB-9272-4247B3A16FD9` |
| 9 | Абсолют | `1F836F31-CF6A-4324-B513-462D4DBEA401` |
| 10 | АстроВолга | `F889BE53-9D60-40B1-97D7-261F4CE66D9C` |
| 11 | Совком | `896D23D9-D567-43B6-B752-80857077A05F` |
| 12 | Энергогарант | `0AD811E1-724B-4502-BFDD-CCE6D0D1ECA5` |
| — | Тинькофф | `0F9D5B82-0D2A-47F4-9DF2-6F5D08E6B30E` |
| — | РЕСО | `3A58D87C-2F5B-4E5F-9A3B-7C8D1E2F3A4B` |
| — | Зетта | `5B1F0E7A-3C2D-4A8E-B6F9-1D2E3F4A5B6C` |
| — | МАКС | `7C2D1E3F-4A5B-6C8D-9E0F-1A2B3C4D5E6F` |
| — | Интач | `8D3E2F4A-5B6C-7D9E-0F1A-2B3C4D5E6F7A` |
| — | Уралсиб | `9E4F3A5B-6C7D-8E0F-1A2B-3C4D5E6F7A8B` |
| — | Росгосстрах | `A0F5B4C6-7D8E-9F0A-1B2C-3D4E5F6A7B8C` |
| — | Чулпан | `B1A6C5D7-8E9F-0A1B-2C3D-4E5F6A7B8C9D` |
| — | ХАККУ | `C2B7D6E8-9F0A-1B2C-3D4E-5F6A7B8C9D0E` |
| — | ОСК | `D3C8E7F9-0A1B-2C3D-4E5F-6A7B8C9D0E1F` |

> InsurerId для СК после ТК12 — ориентировочные. При реальной проверке уточняй через `SELECT InsurerId FROM Insurers WHERE Alias LIKE '%Имя СК%'`.

---

## Логика проверки

### Главное правило
Пришёл пул от СК → система **обязана поставить отказ** (OfferStatusTypeId=3) с OfferType из диапазона (4,6,7,14–21).
Если пул есть, а отказа нет → **баг**.

### Пул без кросса или с добровольным кроссом → **нормально**
Такие кейсы бывают. Главное — отказ.

### Исключение: Альфа и Гелиос
Пул + кросс → система **одобряет** (статус 5 или 6) с вменённым кроссом (IsOptional=false).
- Альфа: UpsaleType=4
- Гелиос: UpsaleType=3
Кросс отсутствует или добровольный → **баг**.

---

## Критические ловушки

### ⚠️ Таблицы с высоким риском таймаута

**VskLogs, RenessansLogs, SoglasieLogs** — очень большие таблицы. Без даты релиза в фильтре скан вешает сервер.
- ВСЕГДА добавляй `AND Date > '[фильтр даты]'`
- Если таймаут — сузь диапазон: `AND Date > DATEADD(day, -3, GETDATE())`
- Не пытайся без даты — не помогут 2-3 ретрая, проблема структурная

### ⚠️ АстроВолга — таймаут на ПРОДЕ через MCP

На проде `AstroVolgaLogs` имеет только кластерный индекс по `Id` (GUID, не хронологический). Date, ApplicationId, Url — не проиндексированы. На ТЕСТЕ таблица небольшая — запросы работают. На ПРОДЕ — ручной запрос пользователем.

### ⚠️ Согласие — XML-паттерн

Правильный фильтр: `LIKE '%<brief>ПерестрахованиеОСАГО</brief><name>Перестрахование ОСАГО</name><result>1</result>%'`

Фильтр `%<id>6463</id>%<result>1</result>%` — ЛОЖНЫЕ СРАБАТЫВАНИЯ: `<result>1</result>` может принадлежать другому XML-элементу после id=6463, не самому пулу.

### ⚠️ Сбер — XML LIKE ложные срабатывания

Любой LIKE-паттерн типа `%dogovor.IsReinsurance%true%` даёт ложные срабатывания: XML содержит много булевых параметров.

Правильный подход: CHARINDEX с точной строкой. Но CHARINDEX без ограничения даты вызывает таймаут. Если 0 строк — это норма, не баг.

### ⚠️ Совком — ложные срабатывания в optionalParameters

Правильный паттерн: `ResponseBody LIKE '%"key": "reinsurancePool"%"value": "true"%'` — требует, чтобы `"value": "true"` шёл именно после `"key": "reinsurancePool"`.

### ⚠️ Широкий поиск — ложные срабатывания

Ключевые слова вроде `reinsurance` или `перестрахов` могут попадаться в:
- Описаниях услуг (не признак пула)
- Полях со значением `null`, `false`, `0`
- Названиях методов/API

Если широкий поиск нашёл попадания — **обязательно проверь содержимое**. Пул = только когда значение истинно (`true`, `1`, не `null`/`false`/`0`).

### ❌ Не используй i.Alias LIKE для фильтра СК

Всегда фильтруй по `i.InsurerId = '[UUID]'`.

### ❌ Не запускай проверку офферов без ApplicationId

Если шаг 1 и шаг 2 вернули 0 заявок — ставь НЕТ ДАННЫХ. Без списка ApplicationId запрос даст некорректные результаты.

### ⚠️ Урок Энергогаранта

Энергогарант добавил признак `is_reinsurance` в API без уведомления. Система не обрабатывала его — заявки одобрялись вместо отказа. Именно для этого существует широкий поиск (шаг 2). **Никогда не пропускай шаг 2**, даже если на шаге 1 все ТК дали НЕТ ДАННЫХ.
