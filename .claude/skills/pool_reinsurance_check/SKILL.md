# Проверка тест-кейсов перестраховочного пула

**Команда:** `/pool-reinsurance-check [дата и время релиза]`

Примеры:
- `/pool-reinsurance-check 2026-03-20 17:00:00`
- `/pool-reinsurance-check 20 марта 2026 17:00`

---

## Алгоритм

### Шаг 0. Разбери аргументы

- **Дата релиза** (обязательно) — подставить в фильтр `AND [table].Date > '[дата релиза]'`
- Если дата не указана — спроси пользователя. Без неё нельзя запустить.

---

### Шаг 1. Запусти шаг 4 по 10 ТК параллельно (найти заявки с признаком пула)

> **ТК10 АстроВолга** — выполняется иначе, см. Шаг 1б ниже.

Запускай запросы **параллельно** (кроме ТК10). Для каждой СК берёшь TOP 10 заявок с признаком пула начиная с даты релиза.

| ТК | СК | Таблица | Фильтр пула |
|---|---|---|---|
| 1 | СОГАЗ | SogazLogs | `Url LIKE '%v1/contracts%' AND ISJSON(ResponseBody)=1 AND JSON_VALUE(ResponseBody,'$.processingResult.commission')='0.1'` |
| 2 | ИГС | IngosLogs | `Url LIKE '%agreement/create%' AND ISJSON(ResponseBody)=1 AND (JSON_VALUE(ResponseBody,'$.warnings[0].warning')='Внимание, уровень 9' OR JSON_VALUE(ResponseBody,'$.warnings[0].warning')='Внимание, уровень 10')` |
| 3 | Альфа | AlfaLogs | `Url LIKE '%osago/calculation%' AND ISJSON(ResponseBody)=1 AND JSON_VALUE(ResponseBody,'$.calculation_details.reinsurance')='true'` |
| 4 | ВСК | VskLogs | `ResponseBody LIKE '%"isReinsurance": true%'` |
| 5 | Гелиос | GeliosLogs | `(Url LIKE '%osago/createcalc%' OR Url LIKE '%gate/getInvoicePaymentUrl%') AND ISJSON(ResponseBody)=1 AND JSON_VALUE(ResponseBody,'$.value.data.result.isReinsured')='true'` |
| 6 | Ренессанс | RenessansLogs | `ResponseBody LIKE '%"isReinsurance": true%'` |
| 7 | Согласие | SoglasieLogs | `ResponseBody LIKE '%<brief>ПерестрахованиеОСАГО</brief><name>Перестрахование ОСАГО</name><result>1</result>%'` |
| 8 | Сбер | SberLogs | см. особые правила ниже |
| 9 | Абсолют | AbsolutLogs | `Url LIKE '%/create%' AND ISJSON(ResponseBody)=1 AND JSON_VALUE(ResponseBody,'$.result.data.reinsurance')='true'` |
| 11 | Совком | SovcomLogs | `ResponseBody LIKE '%"key": "reinsurancePool"%"value": "true"%'` |

**Шаблон запроса (все таблицы кроме ТК10):**
```sql
SELECT TOP 10 ApplicationId, Date
FROM InsappLog.dbo.[Таблица]
WHERE [фильтр пула]
AND Date > '[дата релиза]'
ORDER BY Date DESC
```

Если 0 строк → пометить ТК как **НЕТ ДАННЫХ** и не запускать шаг 6.

---

### Шаг 1б. ТК10 АстроВолга — РУЧНОЙ шаг

**AstroVolgaLogs не имеет индексов на Date/ApplicationId/Url** — любой запрос делает полный скан таблицы и вызывает таймаут через MCP. Это структурное ограничение, не решаемое оптимизацией запроса.

Попроси пользователя выполнить запрос самостоятельно в SQL-клиенте:

```sql
SELECT TOP 10 al.ApplicationId, al.Date
FROM InsappLog.dbo.AstroVolgaLogs al WITH (NOLOCK)
WHERE al.Url LIKE '%/calculator/osago%'
AND ISJSON(al.ResponseBody) = 1
AND JSON_VALUE(al.ResponseBody, '$.data.transferToPool') = 'true'
AND al.Date > '[дата релиза]'
ORDER BY al.Date DESC
```

После того как пользователь вставит ApplicationId — выполни для них шаг 6 (InsappCoreProd работает быстро).

---

### Шаг 2. Запусти шаг 6 — проверить офферы (только для ТК где нашлись заявки)

Для каждого ТК с найденными ApplicationId выполни проверку офферов:

#### ТК 1 (СОГАЗ), 2 (ИГС), 4 (ВСК), 6 (Ренессанс), 7 (Согласие), 8 (Сбер), 9 (Абсолют), 10 (АстроВолга), 11 (Совком)
**Ожидаемый результат:** все офферы имеют `OfferStatusTypeId=3` И `OfferTypeId IN (4,6,7,14,15,16,17,18,19,20,21)`

```sql
SELECT o.ApplicationId, o.OfferStatusTypeId, o.OfferTypeId, i.Alias
FROM InsappCore.dbo.Applications a
JOIN InsappCore.dbo.Offers o ON a.ApplicationId = o.ApplicationId
JOIN InsappCore.dbo.Insurers i ON i.InsurerId = o.InsurerId
WHERE i.InsurerId = '[InsurerId СК]'
AND a.ApplicationId IN ([список ApplicationId из шага 4])
```

Провал: если найдена хоть одна строка с `OfferStatusTypeId != 3` ИЛИ `OfferTypeId NOT IN (4,6,7,14,15,16,17,18,19,20,21)`

#### ТК 3 (Альфа) и ТК 5 (Гелиос)
**Ожидаемый результат:** офферы с `OfferStatusTypeId IN (5,6)` имеют вменённый кросс

```sql
SELECT o.ApplicationId, o.OfferStatusTypeId, o.OfferTypeId, i.Alias, u.UpsaleType, u.IsOptional
FROM InsappCore.dbo.Applications a
JOIN InsappCore.dbo.Offers o ON a.ApplicationId = o.ApplicationId
JOIN InsappCore.dbo.Insurers i ON i.InsurerId = o.InsurerId
LEFT JOIN InsappCore.dbo.Upsales u ON u.OfferId = o.OfferId
WHERE i.InsurerId = '[InsurerId СК]'
AND (o.OfferStatusTypeId = 5 OR o.OfferStatusTypeId = 6)
AND a.ApplicationId IN ([список ApplicationId из шага 4])
```

Провал: если найдена строка без вменённого кросса (`UpsaleType IS NULL` или `IsOptional != 0`) или `OfferTypeId NOT IN (4,6,7,14,15,16,17,18,19,20,21)`
- Альфа: `UpsaleType=4`
- Гелиос: `UpsaleType=3`

---

### Шаг 3. Сформируй отчёт

```
## Проверка пула — [дата релиза]

| ТК | СК | Результат | Комментарий |
|---|---|---|---|
| 1 | СОГАЗ | PASS/FAIL/НЕТ ДАННЫХ | ... |
| 2 | ИГС | ... | ... |
...

### Детали по провальным ТК:
- **ТК1 СОГАЗ** — найдено X заявок с commission=0.1, из них Y имеют OfferStatusTypeId=5
  ApplicationId примера: [id]
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

---

## Критические ловушки

### ⚠️ Таблицы с высоким риском таймаута

**VskLogs, RenessansLogs, SoglasieLogs** — очень большие таблицы. Без даты релиза в фильтре скан вешает сервер.
- ВСЕГДА добавляй `AND Date > '[дата релиза]'`
- Если таймаут — сузь диапазон: `AND Date > DATEADD(day, -3, GETDATE())`
- Не пытайся без даты — не помогут 2-3 ретрая, проблема структурная


### ⚠️ Согласие — XML-паттерн

Правильный фильтр: `LIKE '%<brief>ПерестрахованиеОСАГО</brief><name>Перестрахование ОСАГО</name><result>1</result>%'`

Фильтр `%<id>6463</id>%<result>1</result>%` — ЛОЖНЫЕ СРАБАТЫВАНИЯ: `<result>1</result>` может принадлежать другому XML-элементу после id=6463, не самому пулу.
Фильтр только `%<id>6463</id>%` — ещё хуже, возвращает заявки с `<result>0</result>`.
Привязка к `<brief>ПерестрахованиеОСАГО</brief><name>Перестрахование ОСАГО</name><result>1</result>` однозначно идентифицирует именно пул-признак.

### ⚠️ Сбер — XML LIKE ложные срабатывания

Любой LIKE-паттерн типа `%dogovor.IsReinsurance%true%` или `%dogovor.IsReinsurance%boolValue>true%` даёт ложные срабатывания: XML содержит много булевых параметров, и `true` где-то дальше в документе проходит фильтр, хотя `dogovor.IsReinsurance=false`.

Правильный подход: CHARINDEX с точной строкой `dogovor.IsReinsurance</ns6:name><ns6:type>Логический</ns6:type><ns6:boolValue>true</ns6:boolValue>`. Но CHARINDEX без ограничения даты вызывает таймаут.

На текущий момент (март 2026) в продакшне не было заявок с `dogovor.IsReinsurance=true` — если 0 строк, это норма, не баг.

### ⚠️ Совком — ложные срабатывания в optionalParameters

Структура ответа: `"optionalParameters": [{"key": "reinsurancePool", "name": "...", "value": "true"}]`

Фильтр `%"reinsurancePool"%` AND `%"value":"true"%` — **ЛОЖНЫЕ СРАБАТЫВАНИЯ**: если в массиве есть 100 параметров и хоть один из них (не reinsurancePool) имеет `"value":"true"`, запрос вернёт эту запись.

Правильный паттерн: `ResponseBody LIKE '%"key": "reinsurancePool"%"value": "true"%'` — требует, чтобы `"value": "true"` шёл именно после `"key": "reinsurancePool"` в теле ответа. Поскольку поля внутри одного JSON-объекта идут в порядке key→name→value, это достаточно надёжно.

### ⚠️ АстроВолга — таймаут через MCP, запрос ручной

`AstroVolgaLogs` имеет только кластерный индекс по `Id` (GUID, не хронологический). Date, ApplicationId, Url — не проиндексированы. Любой запрос с фильтром по этим полям делает полный скан и вызывает таймаут.

Шаг 4 для АстроВолга всегда выполняет пользователь вручную в своём SQL-клиенте (см. Шаг 1б).

### ❌ Не используй i.Alias LIKE для фильтра СК

В шаге 6 всегда фильтруй по `i.InsurerId = '[конкретный UUID]'`, не по `i.Alias LIKE '%Sber%'` и т.д. Иначе попадают офферы от нескольких СК с похожим именем.

### ❌ Не запускай шаг 6 без результатов шага 4

Если шаг 4 вернул 0 заявок — ставь НЕТ ДАННЫХ. Шаг 6 без списка ApplicationId даст некорректные результаты.
