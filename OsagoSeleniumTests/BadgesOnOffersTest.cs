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
            screenshot.SaveAsFile(Path.Combine(_screenshotsDir, $"badges_{name}.png"));
            Console.WriteLine($"[SCR] {name}");
        }

        // Возвращает applicationId из URL
        private string GetApplicationId() =>
            _driver.Url.Contains("applicationId=")
                ? _driver.Url.Split("applicationId=")[1].Split('&')[0]
                : null;

        private void LogApplicationId()
        {
            var id = GetApplicationId();
            if (id != null) Console.WriteLine($"  [APPLICATION ID] {id}");
        }

        private void ClickContinueButton()
        {
            _js.ExecuteScript(@"
                var btn = Array.from(document.querySelectorAll('button'))
                    .find(b => b.innerText && b.innerText.trim() === 'Продолжить'
                               && !b.disabled && b.offsetParent !== null);
                if (btn) { btn.scrollIntoView(); btn.click(); }
                else throw new Error('Кнопка Продолжить не найдена');
            ");
            Console.WriteLine("  Нажал Продолжить");
        }

        private bool HasOffer(string companyName) =>
            _driver.FindElements(By.XPath($"//img[contains(@alt,'{companyName}')]"))
                   .Any(e => { try { return e.Displayed; } catch { return false; } });

        private bool HasBadge(string companyName, string badgeText)
        {
            var els = _driver.FindElements(By.XPath(
                $"//*[child::*[normalize-space(text())='{badgeText}'] " +
                $"and descendant::img[contains(@alt,'{companyName}')]]"));
            return els.Any(e => { try { return e.Displayed; } catch { return false; } });
        }

        private bool IsTimerRunning() =>
            _driver.FindElements(By.XPath("//*[contains(normalize-space(text()),'Поиск предложений')]"))
                   .Any(e => { try { return e.Displayed; } catch { return false; } });

        // Вызывает CreateOsagoReport → GetOsagoReport, возвращает insurerName текущей СК
        // Возвращает null если ошибка или forProlongation=false
        private string GetCurrentInsurerName(string applicationId, string licensePlate)
        {
            try
            {
                using var http = new HttpClient();

                // Шаг 1: CreateOsagoReport → получаем requestId
                var createBody = JsonSerializer.Serialize(new
                {
                    apiKey = EnrichmentApiKey,
                    applicationId,
                    licensePlate,
                    clientId = _config.ClientId
                });
                var createJson = http.PostAsync(
                    "https://osago.insapp.ru/enrichment/CreateOsagoReport",
                    new StringContent(createBody, Encoding.UTF8, "application/json")
                ).Result.Content.ReadAsStringAsync().Result;

                var createDoc = JsonDocument.Parse(createJson);
                if (!createDoc.RootElement.TryGetProperty("result", out var cr) || !cr.GetBoolean()) return null;
                var requestId = createDoc.RootElement
                    .GetProperty("value").GetProperty("requestId").GetString();
                Console.WriteLine($"  [OSAGO REPORT] requestId: {requestId}");

                // Шаг 2: GetOsagoReport с retry
                var getBody = JsonSerializer.Serialize(new { apiKey = EnrichmentApiKey, requestId });

                for (var i = 0; i < 6; i++)
                {
                    System.Threading.Thread.Sleep(1500);

                    var getJson = http.PostAsync(
                        "https://osago.insapp.ru/enrichment/GetOsagoReport",
                        new StringContent(getBody, Encoding.UTF8, "application/json")
                    ).Result.Content.ReadAsStringAsync().Result;

                    var getDoc = JsonDocument.Parse(getJson);
                    if (!getDoc.RootElement.TryGetProperty("result", out var gr) || !gr.GetBoolean())
                    {
                        if (getDoc.RootElement.TryGetProperty("error", out var err)
                            && err.ValueKind != JsonValueKind.Null)
                            Console.WriteLine($"  [OSAGO REPORT] Ошибка сервера: {err}");
                        continue;
                    }

                    var value = getDoc.RootElement.GetProperty("value");
                    if (value.ValueKind == JsonValueKind.Null) continue;

                    if (value.TryGetProperty("forProlongation", out var fp) && !fp.GetBoolean())
                    {
                        Console.WriteLine("  [OSAGO REPORT] forProlongation=false — бейдж 'Ваша текущая страховая' не ожидается");
                        return null;
                    }

                    if (!value.TryGetProperty("osagoData", out var od) || od.ValueKind == JsonValueKind.Null)
                    {
                        Console.WriteLine("  [OSAGO REPORT] osagoData отсутствует");
                        return null;
                    }

                    return od.GetProperty("insurerName").GetString();
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [OSAGO REPORT] Ошибка: {ex.Message}");
                return null;
            }
        }

        [Test]
        public void ОСАГО_Отображение_бейджей_на_офферах_СК()
        {
            // ── ШАГ 1: Открыть сайт ──
            Console.WriteLine($"\n[STEP 1] Открываем сайт: {_config.BaseUrl}");
            _driver.Navigate().GoToUrl(_config.BaseUrl);
            _js.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

            // ── ШАГ 2: Записать clientId и перезагрузить ──
            if (!string.IsNullOrEmpty(_config.ClientId))
            {
                Console.WriteLine($"\n[STEP 2] clientId: {_config.ClientId}");
                _js.ExecuteScript($"localStorage.setItem('clientId', '{_config.ClientId}');");
                _driver.Navigate().Refresh();
                _js.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");
            }

            // ── ШАГ 2.5: Ждём прохождения SmartCaptcha ──
            Console.WriteLine("\n[STEP 2.5] Ожидаем SmartCaptcha...");
            var captchaWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(120));
            captchaWait.IgnoreExceptionTypes(typeof(Exception));
            captchaWait.Until(d =>
            {
                var passed = d.FindElements(By.XPath("//*[contains(text(),'Введите номер авто')]"))
                              .Any(e => { try { return e.Displayed; } catch { return false; } });
                if (!passed)
                {
                    var frames = d.FindElements(By.XPath("//iframe[contains(@src,'smartcaptcha') and contains(@src,'advanced')]"));
                    if (frames.Any(e => { try { return e.Displayed; } catch { return false; } }))
                        Console.WriteLine("  [!] Капча-челлендж — решите в браузере...");
                }
                return passed;
            });
            Console.WriteLine("  Капча пройдена");
            TakeScreenshot("01_main_page");

            // ── ШАГ 3: Первая карточка авто ──
            Console.WriteLine("\n[STEP 3] Первая карточка авто");
            var firstCard = _wait.Until(d =>
                d.FindElements(By.XPath("//button[contains(., 'Продолжить')]"))
                 .FirstOrDefault(e => { try { return e.Displayed; } catch { return false; } })
            );
            Assert.That(firstCard, Is.Not.Null, "Карточки авто не найдены");
            Console.WriteLine($"  Карточка: {firstCard.Text.Split('\n')[0].Trim()}");
            _js.ExecuteScript("arguments[0].click();", firstCard);

            new WebDriverWait(_driver, TimeSpan.FromSeconds(30)).Until(d => d.Url.Contains("/form"));
            LogApplicationId();
            TakeScreenshot("02_form");

            // ── ШАГ 4: Данные авто — Продолжить ──
            Console.WriteLine("\n[STEP 4] Данные авто — Продолжить");
            ClickContinueButton();
            System.Threading.Thread.Sleep(1000);

            // ── ШАГ 5: Данные водителя — Продолжить ──
            Console.WriteLine("\n[STEP 5] Данные водителя — Продолжить");
            ClickContinueButton();
            System.Threading.Thread.Sleep(1000);

            // ── ШАГ 6: До /offers (возможен чекбокс согласий) ──
            Console.WriteLine("\n[STEP 6] Идём до офферов");
            for (var attempt = 0; attempt < 5; attempt++)
            {
                if (_driver.Url.Contains("/offers")) break;

                var cb = _driver.FindElements(By.XPath("//input[@type='checkbox']"))
                    .FirstOrDefault(e => { try { return e.Displayed && !e.Selected; } catch { return false; } });
                if (cb != null)
                {
                    _js.ExecuteScript("arguments[0].click();", cb);
                    Console.WriteLine("  Отметил чекбокс");
                }

                ClickContinueButton();
                System.Threading.Thread.Sleep(1000);
            }

            // ── ШАГ 7: Ждём офферы ──
            Console.WriteLine("\n[STEP 7] Ждём офферы");
            new WebDriverWait(_driver, TimeSpan.FromSeconds(60)).Until(d => d.Url.Contains("/offers"));
            new WebDriverWait(_driver, TimeSpan.FromSeconds(60)).Until(d =>
                d.FindElements(By.XPath("//button[normalize-space(text())='Оформить' or normalize-space(text())='Продлить']"))
                 .Any(e => { try { return e.Displayed; } catch { return false; } })
            );
            Console.WriteLine("  Офферы появились");
            TakeScreenshot("07_offers_loaded");

            // ── ШАГ 7.5: Текущая СК через GetOsagoReport ──
            Console.WriteLine("\n[STEP 7.5] Запрашиваем текущего страховщика...");
            var licensePlate = (string)_js.ExecuteScript(
                "try { var a=JSON.parse(localStorage.getItem('insappApp')); " +
                "return JSON.parse(a.data).osagoPolicies[0].osago.carData.licensePlate; } catch(e){return null;}");
            Console.WriteLine($"  LicensePlate: {licensePlate}, ApplicationId: {GetApplicationId()}");

            string currentInsurer = null;
            if (!string.IsNullOrEmpty(licensePlate) && !string.IsNullOrEmpty(_config.ClientId))
            {
                currentInsurer = GetCurrentInsurerName(GetApplicationId(), licensePlate);
                Console.WriteLine($"  Текущая СК: {currentInsurer ?? "не определена"}");
            }

            // ── ШАГ 8: Ждём бейджи ──
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
                Console.WriteLine($"  + 'Ваша текущая страховая' для: {currentInsurer}");
            }

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
                        Console.WriteLine($"  [+] {checks[i].company}: '{checks[i].badge}'");
                    }
                }
                return badgeFound.All(f => f) || !IsTimerRunning();
            });

            TakeScreenshot("08_badges_result");

            // Итоговая проверка
            var warnings = new StringBuilder();
            Assert.Multiple(() =>
            {
                for (var i = 0; i < checks.Count; i++)
                {
                    if (badgeFound[i]) continue;
                    if (HasOffer(checks[i].company))
                        Assert.Fail($"СК '{checks[i].company}' есть в офферах, но бейдж '{checks[i].badge}' отсутствует");
                    else
                        warnings.AppendLine($"  [!] ПРЕДУПРЕЖДЕНИЕ: СК '{checks[i].company}' не появилась в офферах");
                }
            });

            if (warnings.Length > 0) Console.WriteLine("\n" + warnings);
            Console.WriteLine($"  Проверка завершена. URL: {_driver.Url}");
        }
    }
}
