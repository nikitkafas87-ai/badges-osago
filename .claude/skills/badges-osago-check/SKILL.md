---
name: badges-osago-check
description: Проверка подключённых бейджей ОСАГО для списка ApiKey. Собирает данные из БД, строит HTML-отчёт с 3 секциями и фильтрами, шифрует и пушит на GitHub Pages.
---

# Проверка подключённых бейджей ОСАГО

**Команда:** `/badges-osago-check [password:<пароль>]`

---

## Алгоритм

### Шаг 0. Получи список ключей и пароль

Пользователь передаёт только список ApiKey (имена партнёров запрашивать НЕ нужно — они есть в БД).

Если пароль не указан в аргументах — спросить у пользователя (`AskUserQuestion`): «Укажите пароль для доступа к HTML-отчёту».

---

### Шаг 1а. Запрос бейджей

Инструмент: **`mcp__insapp-db__query`** (обязательные параметры: `database`, `sql`, `user_prompt`, `query_description`).
База: **InsappCoreProd**

```sql
WITH badges AS (
    SELECT
        pak.ApiKey,
        p.Name AS PartnerName,
        bt.Name AS BadgeName,
        CASE WHEN i.Alias = 'AlfaDec' THEN i.Name + N' (ДЭК)' ELSE i.Name END AS InsurerName
    FROM PartnerApiKeys pak
    JOIN Partners p ON p.PartnerId = pak.PartnerId
    JOIN PartnerBadgeSetting pbs ON pbs.ApiKeyId = pak.ApiKeyId
    JOIN BadgeTypeSettings bts ON bts.PartnerBadgeSettingId = pbs.PartnerBadgeSettingId
    JOIN BadgeTypes bt ON bt.Id = bts.BadgeTypeId
    JOIN BadgeSettings bs ON bs.PartnerBadgeSettingId = pbs.PartnerBadgeSettingId
    JOIN Insurers i ON i.InsurerId = bs.InsurerId
    WHERE bts.Enabled = 1
      AND bt.Name IN ('APay', 'BestService', 'LoyalInsurer', 'UserChoice')
      AND (
          (bt.Name = 'APay'         AND bs.IsAlfaPayInsurer = 1)
       OR (bt.Name = 'BestService'  AND bs.IsBestService = 1)
       OR (bt.Name = 'LoyalInsurer' AND bs.IsLoyalInsurer = 1)
       OR (bt.Name = 'UserChoice'   AND bs.IsRecomendedInsurer = 1)
      )
      AND pak.ApiKey IN ('ключ1', 'ключ2', ...)

    UNION ALL

    SELECT
        pak.ApiKey,
        p.Name AS PartnerName,
        'UsersInsurer' AS BadgeName,
        NULL AS InsurerName
    FROM PartnerApiKeys pak
    JOIN Partners p ON p.PartnerId = pak.PartnerId
    JOIN PartnerBadgeSetting pbs ON pbs.ApiKeyId = pak.ApiKeyId
    JOIN BadgeTypeSettings bts ON bts.PartnerBadgeSettingId = pbs.PartnerBadgeSettingId
    JOIN BadgeTypes bt ON bt.Id = bts.BadgeTypeId
    WHERE bts.Enabled = 1
      AND bt.Name = 'UsersInsurer'
      AND pak.ApiKey IN ('ключ1', 'ключ2', ...)
)
SELECT * FROM badges
ORDER BY ApiKey, BadgeName, InsurerName
```

---

### Шаг 1б. Запрос имён партнёров для ВСЕХ ключей

**Обязательно** — основной запрос не возвращает партнёров без badge-настроек, их имена нужно получить отдельно.

```sql
SELECT pak.ApiKey, p.Name AS PartnerName
FROM PartnerApiKeys pak
JOIN Partners p ON p.PartnerId = pak.PartnerId
WHERE pak.ApiKey IN ('ключ1', 'ключ2', ...)
```

---

### Шаг 1в. Запрос активности ключей и партнёров

```sql
SELECT pak.ApiKey,
  CASE WHEN pak.IsActive = 1 THEN 'Да' ELSE 'Нет' END AS KeyActive,
  CASE WHEN p.IsActive = 1 THEN 'Да' ELSE 'Нет' END AS PartnerActive
FROM PartnerApiKeys pak
JOIN Partners p ON p.PartnerId = pak.PartnerId
WHERE pak.ApiKey IN ('ключ1', 'ключ2', ...)
```

Это нужно для колонок «Ключ акт.» / «Партнёр акт.» и затемнения неактивных строк (opacity: 0.55).

---

### Шаг 2. Построй HTML-отчёт через Node.js скрипт

Использовать готовый скрипт `.claude/skills/badges-osago-check/build_badges_report.js`. Скрипт:
1. Читает JSON с результатами badge-запроса из файла tool-results
2. Содержит hardcoded список из 91 ApiKey с именами партнёров и activeMap (8 неактивных)
3. Классифицирует ключи на 3 группы: fullBadges, partialBadges, noBadges
4. Генерирует HTML с 3 секциями, каждая со своей таблицей и выпадающими фильтрами
5. Сохраняет на `C:\Users\nikit\Desktop\badges_osago_report.html`

**Запуск:** `node build_badges_report.js`

Если список ключей изменился — обновить `namesRaw` и `activeMap` в скрипте.

> **Альтернатива:** если данных мало (<20 ключей), можно построить HTML напрямую из данных запросов без скрипта. Но для текущих 91 ключа скрипт надёжнее.

---

### Шаг 3. Зашифруй и запушь

Вызвать скилл `/html-push`:

```
/html-push C:\Users\nikit\Desktop\badges_osago_report.html badges-osago password:<пароль>
```

Где `<пароль>` — из Шага 0.

**Важно:** при шифровании удостовериться что в HTML-обёртке **ровно одно** поле `<input>` для пароля. Не добавлять дублирующих полей.

**Push через HTTPS** (не SSH):
```bash
TOKEN=$(git remote get-url origin | grep -oP 'ghp_[a-zA-Z0-9]+')
git remote add origin "https://nikitkafas87-ai:${TOKEN}@github.com/nikitkafas87-ai/badges-osago.git"
git push -u origin main --force
```

SSH может падать с «Host key verification failed» — HTTPS с токеном из remote URL надёжнее.

---

### Шаг 4. Обнови reports-index на GitHub

После пуша отчёта — обновить репозиторий `nikitkafas87-ai/reports-index` (НЕ локальный файл):

1. Скачать `index.html` из репо: `curl -s "https://raw.githubusercontent.com/nikitkafas87-ai/reports-index/main/index.html" > /tmp/reports_index.html`
2. Найти строку с `badges-osago` — обновить дату на текущую `ДД.ММ.ГГГГ`
3. Запушить обновлённый файл через HTTPS:
```bash
TMPIDX=$(mktemp -d)
cp /tmp/reports_index.html "$TMPIDX/index.html"
cd "$TMPIDX"
git init && git checkout -b main
git add index.html
git commit -m "Update badges-osago date"
git remote add origin "https://nikitkafas87-ai:${TOKEN}@github.com/nikitkafas87-ai/reports-index.git"
git push -u origin main --force
```

---

### Шаг 5. Очистка

Удалить `C:\Users\nikit\Desktop\badges_osago_report.html` и `C:\Users\nikit\Desktop\badges_osago_encrypted.html` — файлы уже в GitHub, локальные копии не нужны.

---

## Структура HTML-отчёта

### 3 обязательные секции (ВСЕГДА показывать все 3)

1. **«Все 4 бейджа подключены»** (зелёный заголовок, `h2.full`) — ключи с BestService + LoyalInsurer + UserChoice + UsersInsurer
2. **«Не все бейджи подключены»** (жёлтый заголовок, `h2.partial`) — есть хотя бы 1 бейдж, но не все 4. **Доп. колонка «Не подключены»** с красными тегами отсутствующих бейджей
3. **«Без бейджей — нет настроек»** (красный заголовок, `h2.none`) — ни одного бейджа

Каждая секция содержит:
- Заголовок с количеством
- Счётчик «Показано: N из M»
- Таблицу с выпадающими фильтрами в каждой колонке (dropdown `select` в `<tr class="filter-row">`)
- Если секция пуста — `<div class="empty-msg">Нет партнёров в этой категории</div>`

### Колонки таблиц

| # | Колонка | Описание |
|---|---------|----------|
| 0 | Партнёр | Имя из БД, max-width 200px с усечением |
| 1 | ApiKey | Полный ключ без обрезки, monospace |
| 2 | Ключ акт. | Да/Нет из activeMap |
| 3 | Партнёр акт. | Да/Нет из activeMap |
| 4 | APay | СК или «Нет» |
| 5 | Лучший сервис | СК или «Нет» |
| 6 | Надёжная СК | СК или «Нет» |
| 7 | Выбор поль-лей | СК или «Нет» |
| 8 | Ваша СК | Да/Нет |
| 9 | Не подключены | **Только в partial-секции** — красные теги |

### Неактивные строки

Ключи из `activeMap` с `key: false` или `partner: false` отображаются с `opacity: 0.55`.

### JavaScript фильтры

- Каждый `<select data-col="N">` populated уникальными значениями из колонки
- При изменении — фильтрация строк по точному совпадению текста ячейки
- Обновление счётчика «Показано: N из M»
- Кнопка «Сбросить все фильтры» сбрасывает все select'ы

### Кнопка «Сбросить все фильтры»

`<button class="reset-btn" onclick="resetAll()">Сбросить все фильтры</button>` — перед таблицами.

---

## Структура БД

| Таблица | PK | Связи |
|---|---|---|
| `PartnerApiKeys` | `ApiKeyId` | → `PartnerBadgeSetting` через `ApiKeyId` |
| `PartnerBadgeSetting` | `PartnerBadgeSettingId` | → `BadgeTypeSettings`, `BadgeSettings` через `PartnerBadgeSettingId` |
| `BadgeTypeSettings` | `BadgeTypeSettingId` | `Enabled` — включён ли бейдж; → `BadgeTypes` через `BadgeTypeId` |
| `BadgeSettings` | `BadgeSettingId` | флаги по СК; → `Insurers` через `InsurerId` |
| `BadgeTypes` | `Id` | `Name`: APay, BestService, LoyalInsurer, UserChoice, UsersInsurer, + другие |
| `Insurers` | `InsurerId` | `Name`, `Alias`, `InternalName` |

**Флаги в BadgeSettings:**
| Бейдж | Флаг |
|---|---|
| APay | `IsAlfaPayInsurer = 1` |
| BestService | `IsBestService = 1` |
| LoyalInsurer | `IsLoyalInsurer = 1` |
| UserChoice | `IsRecomendedInsurer = 1` |
| UsersInsurer | флага нет — только `bts.Enabled = 1` |

---

## Критические ловушки

### ❌ Партнёры без бейджей — пустые имена
Основной badge-запрос возвращает только ключи с хотя бы одной записью в `PartnerBadgeSetting`. Ключи без бейджей не появятся совсем → пустые имена в таблице.
**Решение: всегда делать отдельный запрос `SELECT ApiKey, Name FROM PartnerApiKeys JOIN Partners` для ВСЕХ переданных ключей (Шаг 1б).**

### ❌ `mcp__insapp-db__query` — обязательные параметры
Инструмент требует `user_prompt` и `query_description` помимо `database` и `sql`. Без них — ошибка вызова.
**Решение: всегда передавать все 4 параметра.**

### ❌ JOIN BadgeSettings
`BadgeSettings` связан с `PartnerBadgeSetting` через `PartnerBadgeSettingId`, а НЕ с `BadgeTypeSettings`. Неправильный JOIN даёт кросс: одна строка BadgeSettings × все BadgeTypes → несуществующие бейджи в отчёте.

### ❌ Не проверять Enabled
Бейдж может существовать в `BadgeTypeSettings` с `Enabled=false` — он НЕ подключён. Всегда фильтруй `bts.Enabled = 1`.

### ❌ Дублирование полей ввода пароля
При генерации зашифрованной HTML-обёртки **ровно одно** поле `<input type="password">`. Не копировать случайно дважды. Проверять через `grep -c '<input'`.

### ❌ SSH push падает
`git push` через SSH может падать с «Host key verification failed» на Windows.
**Решение:** всегда использовать HTTPS с токеном: `https://nikitkafas87-ai:${TOKEN}@github.com/...`. Токен извлекать из remote URL: `git remote get-url origin | grep -oP 'ghp_[a-zA-Z0-9]+'`.

### ❌ Удаление секций при перезаписи
Все 3 секции (full/partial/none) должны ВСЕГДА присутствовать в отчёте, даже если пустые. При перезаписи HTML не терять секции — это проверять.
**Решение:** каждая секция рендерится отдельно. Пустые секции показывают `<div class="empty-msg">`.

### ⚠️ AlfaDec — одинаковый Name
`Alias='AlfaDec'` и обычная Альфа имеют одинаковый `Name`. При дедупликации без `CASE WHEN i.Alias='AlfaDec'` они сливаются. Различать только через Alias.

### ⚠️ HTML-экранирование
Имена партнёров могут содержать `&`, `<`, `>` — нужно экранировать в HTML (`&amp;`, `&lt;`, `&gt;`). В кавычках типа `АО "ТАНДЕР"` — заменить `"` на `&quot;`.

### ⚠️ Шаблонные литералы в Node.js
При генерации HTML через Node.js **НЕ использовать** template literals (обратные кавычки) — они конфликтуют с HTML и JS внутри. Использовать конкатенацию строк через `+` или массив `parts.join('')`.

### ⚠️ repo-name для /html-push
Использовать `badges-osago` как фиксированное имя репо. При повторных запусках — force push обновит страницу.

---

## Файлы проекта

| Файл | Назначение |
|------|-----------|
| `.claude/skills/badges-osago-check/build_badges_report.js` | Генератор HTML-отчёта из JSON-данных бейджей. Содержит hardcoded 91 ApiKey, activeMap, генерирует 3 секции с фильтрами |
| `reports-index` (GitHub repo) | Индекс всех отчётов на GitHub Pages. Обновлять при каждом пуше отчёта |
| `badges-osago` (GitHub repo) | Целевой репо для зашифрованного HTML-отчёта |

---

## Константы

| Параметр | Значение |
|----------|----------|
| GitHub org | `nikitkafas87-ai` |
| Отчёт repo | `badges-osago` |
| Reports-index repo | `reports-index` |
| URL отчёта | `https://nikitkafas87-ai.github.io/badges-osago/` |
| URL индекса | `https://nikitkafas87-ai.github.io/reports-index/` |
| HTML на Desktop | `C:\Users\nikit\Desktop\badges_osago_report.html` |
| Пароль по умолчанию | спрашивать у пользователя |
