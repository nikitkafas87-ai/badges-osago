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
        private string _apiKey = "";
        private string _urlKey = "";

        private static readonly string[] LandingUrls =
        {
            "https://landing-osago.insapp.ru/",
            "https://osago.ab.insapp.ru/",
            "https://osago.bcs.insapp.ru/",
            "https://osago.gpb.insapp.ru/",
            "https://osago-raiffeisen.insapp.ru/",
            "https://onlinegibdd.ru/strahovanie/osago/",
            "https://osago.gazp.insapp.ru/",
            "https://osago.licard.com/",
            "https://ingos.insapp.ru/",
            "https://osago.avtokod.insapp.ru/",
            "https://osago-tatneft.insapp.ru/",
            "https://ip-semenov-osago.insapp.ru/",
            "https://osago-ubrr.insapp.ru/",
            "https://osago-zenit.insapp.ru/",
            "https://osago-svoy.insapp.ru/",
            "https://osago-azsirbis.insapp.ru/",
            "https://osago-akbars.insapp.ru/",
            "https://osago-podeli.insapp.ru/",
            "https://osago-megafon.insapp.ru/",
            "https://megafon.insapp.ru/",
            "https://osago-rgs.insapp.ru/",
            "https://osago-rgsiframe.insapp.ru/",
            "https://tbank-go.insapp.ru/",
            "https://osago-dolinsk.insapp.ru/",
            "https://osago-mail.insapp.ru/",
            "https://osago-avtodor.insapp.ru/",
            "https://yo.insapp.ru/",
            "https://osago-mfc.insapp.ru/",
            "https://osago-ugoria.insapp.ru/",
            "https://osago-ppr.insapp.ru/",
            "https://osago-nskbl.insapp.ru/",
            "http://osago-drom.insapp.ru/",
            "https://osago-energobank.insapp.ru/",
            "https://wb-go.insapp.ru/",
            "https://osago.trassa.insapp.ru/",
            "http://osago-autopiter.insapp.ru/",
            "http://osago-autospot.insapp.ru/",
            "https://ac-nn.ru/strakhovanie/osago/",
            "https://osago-ruli.insapp.ru/",
            "https://osago.yafuel.insapp.ru/",
            "https://inswift.ru/",
            "https://magnit.insapp.ru/go/",
            "https://ugoria-go.insapp.ru/",
        };

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

            // Перехватываем fetch до загрузки страницы — ловим apiKey из запросов к enrichment API
            ((ChromeDriver)_driver).ExecuteCdpCommand(
                "Page.addScriptToEvaluateOnNewDocument",
                new Dictionary<string, object>
                {
                    ["source"] = @"
                        window.__insappApiKey = null;
                        function _tryExtractKey(body) {
                            try { var b = JSON.parse(body); if (b && b.apiKey) window.__insappApiKey = b.apiKey; } catch(e) {}
                        }
                        const _origFetch = window.fetch;
                        window.fetch = function(url, opts) {
                            if (opts && opts.body) _tryExtractKey(opts.body);
                            return _origFetch.apply(this, arguments);
                        };
                        const _origOpen = XMLHttpRequest.prototype.open;
                        const _origSend = XMLHttpRequest.prototype.send;
                        XMLHttpRequest.prototype.open = function(m, url) { this._url = url; return _origOpen.apply(this, arguments); };
                        XMLHttpRequest.prototype.send = function(body) { if (body) _tryExtractKey(body); return _origSend.apply(this, arguments); };
                    "
                }
            );
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
            screenshot.SaveAsFile(Path.Combine(_screenshotsDir, $"badges_{_urlKey}_{name}.png"));
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

        // Возвращает название СК у которой есть данный бейдж (любая компания)
        private string FindCompanyWithBadge(string badgeText)
        {
            var container = _driver.FindElements(By.XPath(
                $"//*[child::*[normalize-space(text())='{badgeText}'] and descendant::img[@alt]]"))
                .FirstOrDefault(e => { try { return e.Displayed; } catch { return false; } });
            if (container == null) return null;
            var img = container.FindElements(By.XPath(".//img[@alt]"))
                .FirstOrDefault(e => { try { return e.Displayed; } catch { return false; } });
            return img?.GetAttribute("alt");
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
                    apiKey = _apiKey,
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
                var getBody = JsonSerializer.Serialize(new { apiKey = _apiKey, requestId });

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
        [TestCaseSource(nameof(LandingUrls))]
        public void ОСАГО_Отображение_бейджей_на_офферах_СК(string baseUrl)
        {
            _urlKey = new Uri(baseUrl).Host.Replace(".", "-");

            // ── ШАГ 1: Открыть сайт ──
            Console.WriteLine($"\n[STEP 1] Открываем сайт: {baseUrl}");
            _driver.Navigate().GoToUrl(baseUrl);
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
            captchaWait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
            var captchaLogged = false;
            captchaWait.Until(d =>
            {
                var passed = d.FindElements(By.XPath("//*[contains(text(),'Введите номер авто')]"))
                              .Any(e => { try { return e.Displayed; } catch { return false; } });
                if (!passed && !captchaLogged)
                {
                    var frames = d.FindElements(By.XPath("//iframe[contains(@src,'smartcaptcha') and contains(@src,'advanced')]"));
                    if (frames.Any(e => { try { return e.Displayed; } catch { return false; } }))
                    {
                        Console.WriteLine("  [!] Капча-челлендж — решите в браузере...");
                        captchaLogged = true;
                    }
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

            _wait.Until(d => d.Url.Contains("/form"));
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
                System.Threading.Thread.Sleep(3000);
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
            _apiKey = (string)_js.ExecuteScript("return window.__insappApiKey;") ?? "";
            Console.WriteLine($"  ApiKey: {(_apiKey.Length > 0 ? _apiKey : "не найден")}");

            var licensePlate = (string)_js.ExecuteScript(
                "try { var a=JSON.parse(localStorage.getItem('insappApp')); " +
                "return JSON.parse(a.data).osagoPolicies[0].osago.carData.licensePlate; } catch(e){return null;}");
            var applicationId = GetApplicationId();
            Console.WriteLine($"  LicensePlate: {licensePlate}, ApplicationId: {applicationId}");

            string currentInsurer = null;
            if (!string.IsNullOrEmpty(licensePlate) && !string.IsNullOrEmpty(_config.ClientId) && !string.IsNullOrEmpty(_apiKey))
            {
                currentInsurer = GetCurrentInsurerName(applicationId, licensePlate);
                Console.WriteLine($"  Текущая СК: {currentInsurer ?? "не определена"}");
            }

            // ── ШАГ 8: Ждём бейджи ──
            Console.WriteLine("\n[STEP 8] Ждём бейджи...");

            // Бейджи ищем на любой СК — компания определяется динамически
            var badgeTypes = new[] { "Выбор пользователей", "Надежная страховая компания", "Лучший сервис" };
            var badgeCompany = new Dictionary<string, string>(); // badge → company

            var badgeWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(300));
            badgeWait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
            badgeWait.Until(d =>
            {
                foreach (var badge in badgeTypes)
                {
                    if (!badgeCompany.ContainsKey(badge))
                    {
                        var company = FindCompanyWithBadge(badge);
                        if (company != null)
                        {
                            badgeCompany[badge] = company;
                            Console.WriteLine($"  [+] '{badge}': {company}");
                        }
                    }
                }

                // "Ваша текущая страховая" — проверяем на конкретной СК
                if (!string.IsNullOrEmpty(currentInsurer) && !badgeCompany.ContainsKey("Ваша текущая страховая"))
                {
                    if (HasBadge(currentInsurer, "Ваша текущая страховая"))
                    {
                        badgeCompany["Ваша текущая страховая"] = currentInsurer;
                        Console.WriteLine($"  [+] 'Ваша текущая страховая': {currentInsurer}");
                    }
                }

                var allFound = badgeTypes.All(b => badgeCompany.ContainsKey(b)) &&
                               (string.IsNullOrEmpty(currentInsurer) || badgeCompany.ContainsKey("Ваша текущая страховая"));
                return allFound || !IsTimerRunning();
            });

            TakeScreenshot("08_badges_result");

            // Итоговая проверка
            var warnings = new StringBuilder();
            Assert.Multiple(() =>
            {
                foreach (var badge in badgeTypes)
                {
                    if (!badgeCompany.ContainsKey(badge))
                        warnings.AppendLine($"  [!] ПРЕДУПРЕЖДЕНИЕ: бейдж '{badge}' не найден ни на одном оффере");
                }

                if (!string.IsNullOrEmpty(currentInsurer) && !badgeCompany.ContainsKey("Ваша текущая страховая"))
                {
                    if (HasOffer(currentInsurer))
                        Assert.Fail($"СК '{currentInsurer}' есть в офферах, но бейдж 'Ваша текущая страховая' отсутствует");
                    else
                        warnings.AppendLine($"  [!] ПРЕДУПРЕЖДЕНИЕ: текущая СК '{currentInsurer}' не появилась в офферах");
                }
            });

            if (warnings.Length > 0) Console.WriteLine("\n" + warnings);
            Console.WriteLine($"  Проверка завершена. URL: {_driver.Url}");
        }
    }
}
