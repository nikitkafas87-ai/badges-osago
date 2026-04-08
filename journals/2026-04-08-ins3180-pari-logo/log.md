# Задача: Тестирование INS-3180 — Логотип СК Пари в карусели СК
Дата: 2026-04-08
Статус: завершено (перепроверка выполнена, отчёт обновлён)

## План
1. Изучить задачу INS-3180
2. Получить список лендингов из Google Sheets (столбец AV, строки 1-63)
3. Проверить каждый лендинг на наличие логотипа СК Пари
4. Составить отчёт

## Прогресс
### Шаг 1: Изучение задачи
- INS-3180: Добавить логотип СК Пари в карусель СК (ФРОНТ-ДЭВ)
- Статус: Готово к тестированию
- QA: Никита Русанов
- Исполнитель: Иван Дамаскин

### Шаг 2: Сбор URL
- Источник: Google Sheets, лист "ОСАГО", столбец AV, строки 1-63
- Извлечено 69 уникальных URL
- Отфильтрованы "api", "Не нашел", пустые строки

### Шаг 3: Проверка лендингов
Метод: Playwright + JS-evaluate, поиск img[src*='pari'] или img[alt*='Пари']

## Итоги проверки (обновлено после перепроверки)

### Пари НАЙДЕН (32 лендинга):
landing-osago, osago-podeli, bcs, gpb, ubrr, akbars, tbank-go, nskbl, zenit, dolinsk, energobank, gazp, gazp10, gazp5, gazpromneft, tatneft, azsirbis, trassa, yafuel, rgs, avtokod, megafon, megafon2, mail, avtodor, mfc, ppr, drom, autopiter, autospot, ingos, **osagocredit.ab** (company6.png)

### Пари НЕ НАЙДЕН — есть карусель (9 лендингов, БАГ):
osago.ab.insapp.ru, osago-svoy, osago-mlm, osago-lukoil, osago.licard.com, landing-osago-b2b, magnit.insapp.ru/go, **to65.ru**, **paygibdd.ru**

### Нет карусели / другой шаблон (пропуск — 14 лендингов):
benzuber, petrolplus, pn1.shop, prolong-osago, insapp.ru/calculator-osago

### Пустые/iframe-страницы (пропуск):
landing-osago2, osago-b2b, osago-website, osago-b2b-ppr, osago-rgsiframe, osago-ugoria, ugoria-go, ip-semenov-osago, yo

### Внешние с iframe / недоступные (14 сайтов):
lockobank (404), bspb (iframe), itb (битый bitrix-редирект), rgs.ru (iframe), alfa-strah/forma-osago (iframe)

### Недоступные:
osago-yandex (DNS error), tkbbank (timeout), avtocodosago (connection refused), shtrafovnet (требует авторизацию)

### Внешние без карусели (14 сайтов):
onlinegibdd, avtoto, inswift, autoexpert, alfa-strah/prolongation

## Обнаруженные навыки
- Проверка логотипов через Playwright: page.evaluate() + поиск по img[src] и img[alt] — надёжный метод
- Для iframe-страниц: карусель недоступна из родительского фрейма, нужно отдельная проверка
- "interrupted by another navigation" — решается waitUntil: 'load' + увеличенный waitForTimeout
