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

        // Есть ли бейдж с текстом badgeText на карточке СК с именем companyName
        private bool HasBadge(string companyName, string badgeText)
        {
            // Ищем обёртку карточки, у которой есть ПРЯМОЙ дочерний элемент с текстом бейджа
            // и потомок img с alt содержащим название СК
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

            // ── ШАГ 8: Ждём бейджи пока работает таймер ──
            // Таймер = элемент "Поиск предложений" на странице.
            // Если таймер пропал — бейджи больше не придут. Проверяем итог.
            Console.WriteLine("\n[STEP 8] Ждём бейджи (РГС, СОГАЗ, Югория)...");
            bool rgsFound = false, sogazFound = false, yugoriaFound = false;

            var badgeWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(300));
            badgeWait.IgnoreExceptionTypes(typeof(Exception));
            badgeWait.Until(d =>
            {
                if (!rgsFound && HasBadge("Росгосстрах", "Выбор пользователей"))
                {
                    rgsFound = true;
                    Console.WriteLine("  [+] РГС: бейдж 'Выбор пользователей' найден");
                }
                if (!sogazFound && HasBadge("СОГАЗ", "Надежная страховая компания"))
                {
                    sogazFound = true;
                    Console.WriteLine("  [+] СОГАЗ: бейдж 'Надежная страховая компания' найден");
                }
                if (!yugoriaFound && HasBadge("Югория", "Лучший сервис"))
                {
                    yugoriaFound = true;
                    Console.WriteLine("  [+] Югория: бейдж 'Лучший сервис' найден");
                }

                // Все нашли — успех
                if (rgsFound && sogazFound && yugoriaFound) return true;

                // Таймер пропал — офферы больше не придут, выходим для проверки
                if (!IsTimerRunning()) return true;

                return false;
            });

            TakeScreenshot("08_badges_result");

            Assert.Multiple(() =>
            {
                Assert.That(rgsFound,    Is.True, "Бейдж 'Выбор пользователей' не найден у Росгосстрах");
                Assert.That(sogazFound,  Is.True, "Бейдж 'Надежная страховая компания' не найден у СОГАЗ");
                Assert.That(yugoriaFound, Is.True, "Бейдж 'Лучший сервис' не найден у Югория");
            });

            Console.WriteLine("\n  Все бейджи найдены успешно");
            Console.WriteLine($"  URL: {_driver.Url}");
        }
    }
}
