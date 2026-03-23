using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OsagoSeleniumTests.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace OsagoSeleniumTests
{
    [TestFixture]
    public class BadgesOnOffersTest
    {
        private IWebDriver _driver;
        private WebDriverWait _wait;
        private IJavaScriptExecutor _js;
        private TestConfig _config;
        private readonly string _screenshotsDir = @"C:\Users\nikit\projects\autotests\screenshots";
        private const string EnrichmentApiKey = "8ecb0241a4ca4ef7937993db4085dfb7";

        [SetUp]
        public void SetUp()
        {
            _config = TestConfig.Load();
            Directory.CreateDirectory(_screenshotsDir);

            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            // Отключаем флаги автоматизации — снижает вероятность показа капча-челленджа
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);

            _driver = new ChromeDriver(options);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
            _js = (IJavaScriptExecutor)_driver;
        }

        [TearDown]
        public void TearDown()
        {
            _driver?.Quit();
            _driver?.Dispose();
        }

        private void TakeScreenshot(string name)
        {
            var screenshot = ((ITakesScreenshot)_driver).GetScreenshot();
            var path = Path.Combine(_screenshotsDir, $"badges_{name}.png");
            screenshot.SaveAsFile(path);
            Console.WriteLine($"[SCR] {name}");
        }

        private IWebElement WaitForVisible(By by, int timeoutSec = 20)
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSec));
            return wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(by));
        }

        private void LogApplicationId()
        {
            var url = _driver.Url;
            var start = url.IndexOf("applicationId=");
            if (start >= 0)
            {
                start += "applicationId=".Length;
                var end = url.IndexOf('&', start);
                var appId = end >= 0 ? url.Substring(start, end - start) : url.Substring(start);
                Console.WriteLine($"  [APPLICATION ID] {appId}");
            }
        }

        private string ExtractApplicationId(string url)
        {
            var start = url.IndexOf("applicationId=");
            if (start < 0) return null;
            start += "applicationId=".Length;
            var end = url.IndexOf('&', start);
            return end >= 0 ? url.Substring(start, end - start) : url.Substring(start);
        }

        // Считывает гос. номер из localStorage insappApp (актуальный для текущей заявки)
        private string GetLicensePlateFromStorage()
        {
            try
            {
                return (string)_js.ExecuteScript(
                    "try { " +
                    "  var a = JSON.parse(localStorage.getItem('insappApp')); " +
                    "  var d = JSON.parse(a.data); " +
                    "  return d.osagoPolicies[0].osago.carData.licensePlate; " +
                    "} catch(e) { return null; }"
                );
            }
            catch { return null; }
        }

        // Вызывает CreateOsagoReport → GetOsagoReport и возвращает insurerName текущей СК
        private string GetCurrentInsurerName(string applicationId, string licensePlate)
        {
            try
            {
                using var http = new HttpClient();

                // Шаг 1: CreateOsagoReport
                var createBody = JsonSerializer.Serialize(new
                {
                    apiKey = EnrichmentApiKey,
                    applicationId,
                    licensePlate,
                    clientId = _config.ClientId
                });
                var createResp = http.PostAsync(
                    "https://osago.insapp.ru/enrichment/CreateOsagoReport",
                    new StringContent(createBody, Encoding.UTF8, "application/json")
                ).Result;

                var createJson = createResp.Content.ReadAsStringAsync().Result;
                var createDoc = JsonDocument.Parse(createJson);

                if (!createDoc.RootElement.TryGetProperty("result", out var cr) || !cr.GetBoolean())
                    return null;
                if (!createDoc.RootElement.TryGetProperty("value", out var cv))
                    return null;
                if (!cv.TryGetProperty("requestId", out var reqIdProp))
                    return null;
                var requestId = reqIdProp.GetString();
                if (string.IsNullOrEmpty(requestId)) return null;

                Console.WriteLine($"  [OSAGO REPORT] CreateOsagoReport requestId: {requestId}");

                // Шаг 2: GetOsagoReport (с retry, т.к. отчёт может быть ещё не готов)
                var getBody = JsonSerializer.Serialize(new { apiKey = EnrichmentApiKey, requestId });

                for (var i = 0; i < 6; i++)
                {
                    System.Threading.Thread.Sleep(1500);

                    var getResp = http.PostAsync(
                        "https://osago.insapp.ru/enrichment/GetOsagoReport",
                        new StringContent(getBody, Encoding.UTF8, "application/json")
                    ).Result;

                    var getJson = getResp.Content.ReadAsStringAsync().Result;
                    var getDoc = JsonDocument.Parse(getJson);

                    if (!getDoc.RootElement.TryGetProperty("result", out var gr) || !gr.GetBoolean())
                        continue;
                    if (!getDoc.RootElement.TryGetProperty("value", out var gv)
                        || gv.ValueKind == JsonValueKind.Null)
                        continue;
                    if (!gv.TryGetProperty("osagoData", out var od)
                        || od.ValueKind == JsonValueKind.Null)
                        continue;
                    if (!od.TryGetProperty("insurerName", out var iname))
                        continue;

                    return iname.GetString();
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [OSAGO REPORT] Ошибка: {ex.Message}");
                return null;
            }
        }

        // Есть ли карточка СК в списке офферов (независимо от бейджа)
        private bool HasOffer(string companyName)
        {
            var els = _driver.FindElements(By.XPath($"//img[contains(@alt,'{companyName}')]"));
            return els.Any(e => { try { return e.Displayed; } catch { return false; } });
        }

        // Есть ли бейдж с текстом badgeText на карточке СК с именем companyName
        private bool HasBadge(string companyName, string badgeText)
        {
            var xpath = $"//*[" +
                        $"child::*[normalize-space(text())='{badgeText}'] and " +
                        $"descendant::img[contains(@alt,'{companyName}')]" +
                        $"]";
            var els = _driver.FindElements(By.XPath(xpath));
            return els.Any(e => { try { return e.Displayed; } catch { return false; } });
        }

        // Работает ли ещё таймер поиска предложений
        private bool IsTimerRunning()
        {
            var els = _driver.FindElements(By.XPath("//*[contains(normalize-space(text()),'Поиск предложений')]"));
            return els.Any(e => { try { return e.Displayed; } catch { return false; } });
        }

        private void ClickContinueButton()
        {
            _js.ExecuteScript(@"
                var btns = document.querySelectorAll('button');
                var btn = Array.from(btns).find(b =>
                    b.innerText && b.innerText.trim() === 'Продолжить' &&
                    !b.disabled &&
                    b.offsetParent !== null
                );
                if (btn) { btn.scrollIntoView(); btn.click(); }
                else throw new Error('Видимая кнопка Продолжить не найдена');
            ");
            Console.WriteLine("  Нажал Продолжить");
        }

        [Test]
        public void ОСАГО_Отображение_бейджей_на_офферах_СК()
        {
            // ── ШАГ 1: Открыть сайт ──
            Console.WriteLine("\n[STEP 1] Открываем сайт");
            Console.WriteLine($"  URL: {_config.BaseUrl}");
            _driver.Navigate().GoToUrl(_config.BaseUrl);
            // Убираем признак WebDriver — SmartCaptcha реже показывает challenge
            _js.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

            // ── ШАГ 2: Записать clientId в localStorage и перезагрузить ──
            if (!string.IsNullOrEmpty(_config.ClientId))
            {
                Console.WriteLine($"\n[STEP 2] Записываем clientId в localStorage: {_config.ClientId}");
                _js.ExecuteScript($"localStorage.setItem('clientId', '{_config.ClientId}');");
                _driver.Navigate().Refresh();
                _js.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");
                Console.WriteLine("  Страница перезагружена");
            }

            // ── ШАГ 2.5: Ждём капчу ──
            // Обычно SmartCaptcha авто-валидируется (~3-5 сек).
            // Если появился визуальный challenge — он виден в браузере, пользователь решает вручную.
            // Даём до 120 секунд на случай ручного решения капчи.
            Console.WriteLine("\n[STEP 2.5] Ожидаем прохождения SmartCaptcha...");
            var captchaWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(120));
            captchaWait.IgnoreExceptionTypes(typeof(Exception));
            captchaWait.Until(d =>
            {
                // Ждём пока появится текст "Введите номер авто" — признак что капча пройдена
                var els = d.FindElements(By.XPath("//*[contains(text(),'Введите номер авто')]"));
                var passed = els.Any(e => { try { return e.Displayed; } catch { return false; } });
                if (!passed)
                {
                    // Проверяем есть ли видимый капча-challenge (iframe с контентом)
                    var captchaFrames = d.FindElements(By.XPath("//iframe[contains(@src,'smartcaptcha') and contains(@src,'advanced')]"));
                    if (captchaFrames.Any(e => { try { return e.Displayed; } catch { return false; } }))
                        Console.WriteLine("  [!] Обнаружен капча-челлендж — решите капчу в браузере...");
                }
                return passed;
            });
            Console.WriteLine("  Капча пройдена, страница загружена");
            TakeScreenshot("01_main_page");

            // ── ШАГ 3: Выбрать первую карточку авто ──
            Console.WriteLine("\n[STEP 3] Выбираем первую карточку авто");
            var firstCard = _wait.Until(d =>
                d.FindElements(By.XPath("//button[contains(., 'Продолжить')]"))
                 .FirstOrDefault(e => { try { return e.Displayed; } catch { return false; } })
            );
            Assert.That(firstCard, Is.Not.Null, "Карточки авто не найдены");
            Console.WriteLine($"  Выбрана карточка: {firstCard.Text.Split('\n')[0].Trim()}");
            _js.ExecuteScript("arguments[0].click();", firstCard);

            // Ждём перехода на /form
            new WebDriverWait(_driver, TimeSpan.FromSeconds(30)).Until(d => d.Url.Contains("/form"));
            LogApplicationId();
            TakeScreenshot("02_form_car");

            // ── ШАГ 4: Вкладка "Данные авто" — Продолжить ──
            Console.WriteLine("\n[STEP 4] Вкладка 'Данные авто' — Продолжить");
            ClickContinueButton();
            System.Threading.Thread.Sleep(2000);
            TakeScreenshot("03_form_driver");

            // ── ШАГ 5: Вкладка "Данные водителя" — Продолжить ──
            Console.WriteLine("\n[STEP 5] Вкладка 'Данные водителя' — Продолжить");
            ClickContinueButton();
            System.Threading.Thread.Sleep(2000);
            TakeScreenshot("04_form_contacts");

            // ── ШАГ 6: Кликаем Продолжить до появления чекбокса или URL /offers ──
            Console.WriteLine("\n[STEP 6] Идём до контактов");
            for (var attempt = 0; attempt < 5; attempt++)
            {
                TakeScreenshot($"06_attempt_{attempt + 1:D2}");
                Console.WriteLine($"  Попытка {attempt + 1}, URL: {_driver.Url}");

                if (_driver.Url.Contains("/offers")) break;

                var visibleCheckbox = _driver.FindElements(By.XPath("//input[@type='checkbox']"))
                    .FirstOrDefault(e => { try { return e.Displayed && !e.Selected; } catch { return false; } });

                if (visibleCheckbox != null)
                {
                    Console.WriteLine("  Найден чекбокс — отмечаем");
                    _js.ExecuteScript("arguments[0].click();", visibleCheckbox);
                    System.Threading.Thread.Sleep(500);
                }

                ClickContinueButton();
                System.Threading.Thread.Sleep(2000);
            }

            // ── ШАГ 7: Ждём страницу офферов ──
            Console.WriteLine("\n[STEP 7] Ждём офферы");
            new WebDriverWait(_driver, TimeSpan.FromSeconds(60)).Until(d => d.Url.Contains("/offers"));
            new WebDriverWait(_driver, TimeSpan.FromSeconds(60)).Until(d =>
                d.FindElements(By.XPath("//button[normalize-space(text())='Оформить' or normalize-space(text())='Продлить']"))
                 .Any(e => { try { return e.Displayed; } catch { return false; } })
            );
            Console.WriteLine("  Офферы появились");
            TakeScreenshot("07_offers_loaded");

            // ── ШАГ 7.5: Определяем текущую СК через GetOsagoReport ──
            Console.WriteLine("\n[STEP 7.5] Запрашиваем текущего страховщика (GetOsagoReport)...");
            var applicationId = ExtractApplicationId(_driver.Url);
            var licensePlate = GetLicensePlateFromStorage();
            Console.WriteLine($"  ApplicationId: {applicationId}");
            Console.WriteLine($"  LicensePlate: {licensePlate}");

            string currentInsurer = null;
            if (!string.IsNullOrEmpty(applicationId) && !string.IsNullOrEmpty(licensePlate)
                && !string.IsNullOrEmpty(_config.ClientId))
            {
                currentInsurer = GetCurrentInsurerName(applicationId, licensePlate);
                Console.WriteLine($"  Текущая СК: {currentInsurer ?? "не определена"}");
            }

            // ── ШАГ 8: Ждём бейджи пока работает таймер ──
            // Логика:
            //   - СК есть в офферах, но бейдж отсутствует → FAIL
            //   - СК вообще нет в офферах → WARNING (тест проходит)
            Console.WriteLine("\n[STEP 8] Ждём бейджи...");

            var checks = new List<(string company, string badge)>
            {
                ("Росгосстрах", "Выбор пользователей"),
                ("СОГАЗ",       "Надежная страховая компания"),
                ("Югория",      "Лучший сервис"),
            };

            if (!string.IsNullOrEmpty(currentInsurer))
            {
                checks.Add((currentInsurer, "Ваша текущая страховая"));
                Console.WriteLine($"  Добавлена проверка 'Ваша текущая страховая' для: {currentInsurer}");
            }

            // Для каждой СК храним: нашли ли бейдж
            var badgeFound = new bool[checks.Count];

            var badgeWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(300));
            badgeWait.IgnoreExceptionTypes(typeof(Exception));
            badgeWait.Until(d =>
            {
                for (var i = 0; i < checks.Count; i++)
                {
                    if (!badgeFound[i] && HasBadge(checks[i].company, checks[i].badge))
                    {
                        badgeFound[i] = true;
                        Console.WriteLine($"  [+] {checks[i].company}: бейдж '{checks[i].badge}' найден");
                    }
                }

                if (badgeFound.All(f => f)) return true;   // все нашли
                if (!IsTimerRunning()) return true;         // таймер кончился
                return false;
            });

            TakeScreenshot("08_badges_result");

            // Итоговая проверка по каждой СК
            var warnings = new StringBuilder();
            Assert.Multiple(() =>
            {
                for (var i = 0; i < checks.Count; i++)
                {
                    if (badgeFound[i]) continue;

                    if (HasOffer(checks[i].company))
                    {
                        // СК есть, бейджа нет → FAIL
                        Assert.Fail($"СК '{checks[i].company}' есть в офферах, но бейдж '{checks[i].badge}' отсутствует");
                    }
                    else
                    {
                        // СК нет вообще → WARNING
                        warnings.AppendLine($"  [!] ПРЕДУПРЕЖДЕНИЕ: СК '{checks[i].company}' не появилась в офферах");
                    }
                }
            });

            if (warnings.Length > 0)
                Console.WriteLine("\n" + warnings);

            Console.WriteLine("  Проверка бейджей завершена");
            Console.WriteLine($"  URL: {_driver.Url}");
        }
    }
}
