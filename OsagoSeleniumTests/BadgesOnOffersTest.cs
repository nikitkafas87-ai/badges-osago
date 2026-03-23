using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OsagoSeleniumTests.Config;
using System;
using System.IO;
using System.Linq;

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

        [SetUp]
        public void SetUp()
        {
            _config = TestConfig.Load();
            Directory.CreateDirectory(_screenshotsDir);

            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");

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

            // ── ШАГ 2: Записать clientId в localStorage и перезагрузить ──
            if (!string.IsNullOrEmpty(_config.ClientId))
            {
                Console.WriteLine($"\n[STEP 2] Записываем clientId в localStorage: {_config.ClientId}");
                _js.ExecuteScript($"localStorage.setItem('clientId', '{_config.ClientId}');");
                _driver.Navigate().Refresh();
                Console.WriteLine("  Страница перезагружена");
            }

            // Ждём пока SmartCaptcha авто-валидируется и страница загрузится (до 60 сек)
            WaitForVisible(By.XPath("//*[contains(text(),'Введите номер авто')]"), 60);
            Console.WriteLine("  Капча прошла, страница загружена");
            TakeScreenshot("01_main_page");

            // ── ШАГ 3: Выбрать карточку Renault ──
            Console.WriteLine("\n[STEP 3] Выбираем карточку Renault");
            var renaultCard = _wait.Until(d =>
                d.FindElements(By.XPath("//button[contains(., 'Renault') and contains(., 'Продолжить')]"))
                 .FirstOrDefault(e => { try { return e.Displayed; } catch { return false; } })
            );
            Assert.That(renaultCard, Is.Not.Null, "Карточка Renault не найдена");
            _js.ExecuteScript("arguments[0].click();", renaultCard);
            Console.WriteLine("  Карточка Renault выбрана");

            // Ждём перехода на /form
            new WebDriverWait(_driver, TimeSpan.FromSeconds(30)).Until(d => d.Url.Contains("/form"));
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
            TakeScreenshot("05_offers_page");
            Console.WriteLine($"  URL: {_driver.Url}");
        }
    }
}
