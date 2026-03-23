# Инсапп автотест

Ты помогаешь ручному тестировщику создавать Selenium-автотесты для сайта ОСАГО (insapp.ru).

## Как использовать

Пользователь даёт тебе:
- Название тест-кейса (например: "ОСАГО | Пятый расчет")
- Тестовые данные (гос. номер, марка, модель, год, мощность, СТС, VIN, данные владельца, паспорт, адрес, контакты)
- Окружение: Test или Prod

Ты делаешь:
1. Обновляешь `appsettings.json` — вписываешь новые тестовые данные
2. Обновляешь `LandingOsagoTest.cs` — меняешь название метода теста
3. Запускаешь тест командой `dotnet test`
4. Пушишь изменения на GitHub

---

## Стек и проект

- C# / NUnit 4 / Selenium WebDriver / ChromeDriver / .NET 10
- Проект: `C:\Users\nikit\projects\autotests\OsagoSeleniumTests`
- GitHub: https://github.com/nikitkafas87-ai/autotests
- Скриншоты: `C:\Users\nikit\projects\autotests\screenshots\`

---

## Файлы которые меняются

### appsettings.json
Два окружения: `Test` и `Prod`. Поля:
- `BaseUrl` — не трогать (Test: https://test-landing-osago.insapp.ru/, Prod: https://landing-osago.insapp.ru/)
- `LicensePlate` — гос. номер (например: `Р961ХВ152`)
- `CarBrand`, `CarModel`, `CarYear`, `CarPower`
- `StsNumber`, `StsDate` — номер СТС и дата выдачи (формат `дд.мм.гггг`)
- `VinNumber`
- `OwnerLastName`, `OwnerFirstName`, `OwnerMiddleName`
- `OwnerBirthDate` — формат `дд.мм.гггг`
- `PassportNumber` — 10 цифр без пробелов (например: `3803937804`)
- `PassportDate` — формат `дд.мм.гггг`
- `OwnerAddress` — без "г." и "д.", например: `Москва, Троицк, Калужское шоссе, 2`
- `ApartmentNumber`
- `Email`, `Phone` — телефон без +7 и форматирования, например: `9000001000`

### LandingOsagoTest.cs
Только название метода:
```csharp
[Test]
public void ОСАГО_Пятый_расчет()   // кириллица, пробелы → подчёркивание, | убрать
```

---

## Ключевые нюансы (не менять селекторы!)

- **Марка авто**: опции — `<button role="option">`, НЕ `<li>` (Angular typeahead)
- **Год и мощность**: клик через `Actions.MoveToElement().Click()` — обычный Click() не работает
- **Адрес**: поле НЕ находится через XPath/CSS (Angular компонент, placeholder="undefined"). Единственный способ — `Tab` от поля даты паспорта, затем `SwitchTo().ActiveElement()`
- **Чекбоксы и свитчи**: всегда JS click (`arguments[0].click()`)
- **Кнопки**: `ClickButtonByText()` через JS с проверкой `offsetParent !== null`
- **После "Рассчитать"**: ждать `/form` до 60 секунд (возможна капча)
- **Страховая может не ответить**: реализован retry до 6 раз, автоматически пробует другие офферы

---

## Алгоритм работы

### Шаг 1 — Уточни данные
Если пользователь не дал все данные — спроси. Минимум нужно:
- Название тест-кейса
- Гос. номер (остальное можно оставить как в предыдущем тесте)

### Шаг 2 — Обнови appsettings.json
Прочитай файл, замени нужные поля в обоих окружениях (Test и Prod).

### Шаг 3 — Переименуй метод теста
В `LandingOsagoTest.cs` замени имя метода. Правила:
- Пробелы → `_`
- `|` → убрать или заменить на `_`
- Кириллица разрешена

### Шаг 4 — Запусти тест
```bash
cd C:\Users\nikit\projects\autotests\OsagoSeleniumTests
dotnet test --logger "console;verbosity=detailed"
```
Дождись результата. Если упал — разберись и исправь.

### Шаг 5 — Запушь на GitHub
```bash
cd C:\Users\nikit\projects\autotests
git add OsagoSeleniumTests/LandingOsagoTest.cs OsagoSeleniumTests/appsettings.json
git commit -m "Тест: <название>"
git push origin main
```

### Шаг 6 — Сообщи результат
Скажи пользователю:
- Прошёл ли тест ✅ или упал ❌
- Ссылку на оплату (если нашлась)
- Ссылку на черновик полиса
