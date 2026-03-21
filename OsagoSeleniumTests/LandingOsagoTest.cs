using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OsagoSeleniumTests.Config;
using OpenQA.Selenium.Interactions;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace OsagoSeleniumTests
{
    [TestFixture]
    public class LandingOsagoTest
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
            var path = Path.Combine(_screenshotsDir, $"landing_{name}.png");
            screenshot.SaveAsFile(path);
            Console.WriteLine($"[SCR] {name}");
        }

        private IWebElement WaitForVisible(By by, int timeoutSec = 20)
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSec));
            return wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(by));
        }

        private void ClickButtonByText(string text)
        {
            _js.ExecuteScript($@"
                var btns = document.querySelectorAll('button');
                var btn = Array.from(btns).find(b => b.innerText && b.innerText.includes('{text}') && b.offsetParent !== null);
                if (btn) btn.click();
                else throw new Error('Button not found: {text}');
            ");
            Console.WriteLine($"  Нажал \"{text}\"");
            Thread.Sleep(2000);
        }

        [Test]
        public void FullLandingOsagoFlow()
        {
            // ── ШАГ 1: Открыть сайт ──
            Console.WriteLine("\n[STEP 1] Открываем сайт");
            Console.WriteLine($"  URL: {_config.BaseUrl}");
            _driver.Navigate().GoToUrl(_config.BaseUrl);

            WaitForVisible(By.XPath("//button[contains(text(),'Рассчитать')]"), 30);
            TakeScreenshot("01_main_page");

            // ── ШАГ 2: Ввод гос. номера ──
            Console.WriteLine("\n[STEP 2] Вводим гос. номер: " + _config.LicensePlate);
            var plateInput = WaitForVisible(By.CssSelector("input[placeholder='A 000 AA 000']"), 30);
            plateInput.Click();
            plateInput.Clear();
            plateInput.SendKeys(_config.LicensePlate);
            TakeScreenshot("02_plate_entered");

            ClickButtonByText("Рассчитать");
            // Ждём перехода на страницу формы (капча может занимать время)
            new WebDriverWait(_driver, TimeSpan.FromSeconds(60)).Until(d => d.Url.Contains("/form"));
            TakeScreenshot("03_after_calculate");
            Console.WriteLine($"  URL: {_driver.Url}");

            // ── ШАГ 3: Марка авто ──
            Console.WriteLine("\n[STEP 3] Вводим марку: " + _config.CarBrand);
            var brandInput = WaitForVisible(By.Id("brandId"));
            brandInput.Click();
            brandInput.SendKeys(_config.CarBrand);

            var brandOption = _wait.Until(d =>
                d.FindElements(By.XPath($"//button[@role='option' and normalize-space(.)='{_config.CarBrand}']"))
                 .FirstOrDefault(o => { try { return o.Displayed; } catch { return false; } })
            );
            brandOption!.Click();
            Console.WriteLine($"  Выбрал марку: {_config.CarBrand}");
            TakeScreenshot("04_brand_selected");

            // ── ШАГ 4: Модель авто ──
            Console.WriteLine("\n[STEP 4] Вводим модель: " + _config.CarModel);
            var modelInput = _wait.Until(d => {
                var el = d.FindElement(By.Id("modelId"));
                return el.Enabled && el.Displayed ? el : null;
            });
            modelInput!.Click();
            modelInput.SendKeys(_config.CarModel);

            var modelOption = _wait.Until(d =>
                d.FindElements(By.XPath($"//li[starts-with(normalize-space(.), '{_config.CarModel}')]"))
                 .FirstOrDefault(o => o.Displayed)
            );
            modelOption!.Click();
            Console.WriteLine($"  Выбрал модель: {_config.CarModel}");
            TakeScreenshot("05_model_selected");

            // ── ШАГ 5: Год выпуска ──
            Console.WriteLine("\n[STEP 5] Вводим год: " + _config.CarYear);
            var yearInput = _wait.Until(d => {
                var el = d.FindElement(By.Id("productionYear"));
                return el.Enabled && el.Displayed ? el : null;
            });
            yearInput!.Click();
            yearInput.SendKeys(_config.CarYear);

            var yearOption = _wait.Until(d =>
                d.FindElements(By.XPath($"//li[starts-with(normalize-space(.), '{_config.CarYear}')]"))
                 .FirstOrDefault(o => o.Displayed)
            );
            new Actions(_driver).MoveToElement(yearOption!).Click().Perform();
            Console.WriteLine($"  Выбрал год: {_config.CarYear}");
            TakeScreenshot("06_year_selected");

            // ── ШАГ 6: Мощность ──
            Console.WriteLine("\n[STEP 6] Выбираем мощность: " + _config.CarPower);
            var powerInput = _wait.Until(d => {
                var el = d.FindElement(By.Id("horsePower"));
                return el.Enabled && el.Displayed ? el : null;
            });
            powerInput!.Click();

            var powerOption = _wait.Until(d =>
                d.FindElements(By.XPath($"//li[starts-with(normalize-space(.), '{_config.CarPower}')]"))
                 .FirstOrDefault(o => o.Displayed)
            );
            new Actions(_driver).MoveToElement(powerOption!).Click().Perform();
            Console.WriteLine($"  Выбрал мощность: {_config.CarPower}");
            TakeScreenshot("07_power_selected");

            // ── ШАГ 7: СТС, дата СТС, VIN ──
            Console.WriteLine("\n[STEP 7] Заполняем СТС, дату и VIN");

            var stsInput = WaitForVisible(By.Name("stsNumberControlName"));
            stsInput.Click();
            stsInput.SendKeys(_config.StsNumber);
            Console.WriteLine($"  СТС: {_config.StsNumber}");
            Thread.Sleep(500);

            var stsDateInput = WaitForVisible(By.CssSelector("input[placeholder='Дата выдачи СТС']"));
            stsDateInput.Click();
            stsDateInput.SendKeys(_config.StsDate);
            Console.WriteLine($"  Дата СТС: {_config.StsDate}");

            var vinInput = WaitForVisible(By.Name("vinNumberControlName"));
            vinInput.Click();
            vinInput.SendKeys(_config.VinNumber);
            Console.WriteLine($"  VIN: {_config.VinNumber}");
            TakeScreenshot("08_docs_filled");

            // ── ШАГ 8: Продолжить — данные авто ──
            Console.WriteLine("\n[STEP 8] Продолжить (данные авто)");
            ClickButtonByText("Продолжить");
            Thread.Sleep(3000);
            TakeScreenshot("09_after_car_continue");

            // ── ШАГ 9: Водитель — ставим "Без ограничений" ──
            Console.WriteLine("\n[STEP 9] Водитель: без ограничений");
            var driverUnlimited = WaitForVisible(By.Id("driversWithoutRestrictionControlName"));
            if (!driverUnlimited.Selected)
                _js.ExecuteScript("arguments[0].click();", driverUnlimited);
            Console.WriteLine("  Без ограничений: включён");
            Thread.Sleep(1000);
            TakeScreenshot("10_driver_unlimited");

            ClickButtonByText("Продолжить");
            Thread.Sleep(3000);
            TakeScreenshot("11_owner_tab");

            // ── ШАГ 10: Собственник ──
            Console.WriteLine("\n[STEP 10] Заполняем данные собственника");

            var ownerLastName = WaitForVisible(By.Id("ownerLastName"));
            ownerLastName.Click();
            ownerLastName.SendKeys(_config.OwnerLastName);
            Thread.Sleep(700);
            ownerLastName.SendKeys(Keys.Tab);
            Console.WriteLine($"  Фамилия: {_config.OwnerLastName}");

            var ownerFirstName = WaitForVisible(By.Id("ownerFirstName"));
            ownerFirstName.Click();
            ownerFirstName.SendKeys(_config.OwnerFirstName);
            Thread.Sleep(700);
            ownerFirstName.SendKeys(Keys.Tab);
            Console.WriteLine($"  Имя: {_config.OwnerFirstName}");

            var ownerMiddleName = WaitForVisible(By.Id("ownerMiddleName"));
            ownerMiddleName.Click();
            ownerMiddleName.SendKeys(_config.OwnerMiddleName);
            Thread.Sleep(700);
            ownerMiddleName.SendKeys(Keys.Tab);
            Console.WriteLine($"  Отчество: {_config.OwnerMiddleName}");

            var ownerBirthDate = WaitForVisible(By.Name("ownerBirthDateControlName"));
            ownerBirthDate.Click();
            ownerBirthDate.SendKeys(_config.OwnerBirthDate);
            Console.WriteLine($"  Дата рождения: {_config.OwnerBirthDate}");
            Thread.Sleep(500);

            var passportInput = WaitForVisible(By.Name("passportLicenseControlName"));
            passportInput.Click();
            passportInput.SendKeys(_config.PassportNumber);
            Console.WriteLine($"  Паспорт: {_config.PassportNumber}");
            Thread.Sleep(500);

            var passportDateInput = WaitForVisible(By.CssSelector("input[placeholder='Дата выдачи паспорта']"));
            passportDateInput.Click();
            passportDateInput.SendKeys(_config.PassportDate);
            Console.WriteLine($"  Дата выдачи паспорта: {_config.PassportDate}");
            Thread.Sleep(500);

            TakeScreenshot("12_before_address");

            // Адрес регистрации — поле в особом компоненте, переходим через Tab после даты паспорта
            passportDateInput.SendKeys(Keys.Tab);
            Thread.Sleep(500);
            var addressInput = _driver.SwitchTo().ActiveElement();
            addressInput.SendKeys(_config.OwnerAddress);
            Thread.Sleep(2000);
            TakeScreenshot("12_address_typed");

            // Выбираем первую подсказку из Dadata/typeahead
            var addrOption = _wait.Until(d =>
                d.FindElements(By.XPath(
                    "//typeahead-container//button | //typeahead-container//li | " +
                    "//ul[contains(@class,'dropdown-menu')]//li//button | " +
                    "//ul[contains(@class,'dropdown-menu')]//li//a | " +
                    "//div[contains(@class,'suggestion')]"))
                 .FirstOrDefault(o => { try { return o.Displayed; } catch { return false; } })
            );
            if (addrOption != null)
            {
                addrOption.Click();
                Console.WriteLine($"  Адрес: {_config.OwnerAddress}");
            }
            else
            {
                Console.WriteLine("  [WARN] Подсказка адреса не появилась");
                TakeScreenshot("12_address_no_suggestion");
            }

            var aptInput = WaitForVisible(By.Name("ownerHouseNumberControlName"));
            aptInput.Click();
            aptInput.SendKeys(_config.ApartmentNumber);
            Console.WriteLine($"  Квартира: {_config.ApartmentNumber}");
            Thread.Sleep(500);

            // Свитч "Собственник является страхователем"
            var ownerIsInsurerSwitch = WaitForVisible(By.Id("nullControlName"));
            if (!ownerIsInsurerSwitch.Selected)
                _js.ExecuteScript("arguments[0].click();", ownerIsInsurerSwitch);
            Console.WriteLine("  Собственник = страхователь: включён");

            TakeScreenshot("13_owner_filled");

            ClickButtonByText("Продолжить");
            Thread.Sleep(3000);
            TakeScreenshot("14_contacts_tab");

            // ── ШАГ 11: Контакты ──
            Console.WriteLine("\n[STEP 11] Заполняем контакты");

            var emailInput = WaitForVisible(By.Name("emailControlName"));
            emailInput.Click();
            emailInput.SendKeys(_config.Email);
            Console.WriteLine($"  Email: {_config.Email}");

            var phoneInput = WaitForVisible(By.Name("phoneControlName"));
            phoneInput.Click();
            phoneInput.SendKeys(_config.Phone);
            Console.WriteLine($"  Телефон: {_config.Phone}");

            Thread.Sleep(1000);
            TakeScreenshot("15_before_agreements");

            // Чекбоксы согласий
            var agreementCheckboxes = _driver.FindElements(By.CssSelector("input[type='checkbox']"))
                .Where(cb => { try { return cb.Displayed && !cb.Selected; } catch { return false; } })
                .ToList();
            foreach (var cb in agreementCheckboxes)
                _js.ExecuteScript("arguments[0].click();", cb);
            Console.WriteLine($"  Чекбоксов согласий отмечено: {agreementCheckboxes.Count}");
            TakeScreenshot("16_agreements_checked");

            ClickButtonByText("Продолжить");
            Thread.Sleep(7000);
            TakeScreenshot("17_offers_page");

            // ── ШАГ 12: Выбираем оффер ──
            Console.WriteLine("\n[STEP 12] Выбираем оффер");
            var offerButton = _wait.Until(d =>
                d.FindElements(By.XPath("//button[contains(normalize-space(.),'Оформить') or contains(normalize-space(.),'Выбрать') or contains(normalize-space(.),'Купить')]"))
                 .FirstOrDefault(o => { try { return o.Displayed; } catch { return false; } })
            );
            Assert.That(offerButton, Is.Not.Null, "Кнопка выбора оффера не найдена");
            offerButton!.Click();
            Console.WriteLine("  Оффер выбран");
            Thread.Sleep(7000);
            TakeScreenshot("18_after_offer");

            // ── ШАГ 13: Проверка данных — нажимаем "Все верно" ──
            Console.WriteLine("\n[STEP 13] Страница проверки данных — нажимаем 'Все верно'");
            TakeScreenshot("19_verify_page");

            ClickButtonByText("Все верно");
            Thread.Sleep(7000);
            TakeScreenshot("20_payment_page");
            Console.WriteLine($"  URL: {_driver.Url}");

            // ── ШАГ 14: Ждём ссылку на оплату (с retry при отказе страховой) ──
            Console.WriteLine("\n[STEP 14] Ждём ссылку на оплату (retry при отказе страховой)");

            IWebElement? paymentElement = null;
            var maxAttempts = 6;

            for (var attempt = 0; attempt < maxAttempts && paymentElement == null; attempt++)
            {
                if (attempt > 0)
                    Console.WriteLine($"\n  === Попытка {attempt + 1}/{maxAttempts} ===");

                // Ждём до 3 минут: либо ссылка оплаты, либо «не ответила»
                var pollStart = DateTime.Now;

                while ((DateTime.Now - pollStart).TotalSeconds < 180)
                {
                    Thread.Sleep(5000);

                    var pageText = "";
                    try { pageText = _driver.FindElement(By.TagName("body")).Text; } catch { }

                    paymentElement = _driver.FindElements(By.XPath("//button | //a"))
                        .Where(e => { try { return e.Displayed; } catch { return false; } })
                        .FirstOrDefault(e => {
                            try {
                                var text = e.Text.ToLower();
                                var href = e.GetAttribute("href") ?? "";
                                return text.Contains("оплат") || text.Contains("перейти к оплате") ||
                                       href.Contains("pay") || href.Contains("checkout") ||
                                       href.Contains("bill") || href.Contains("acquiring");
                            } catch { return false; }
                        });

                    if (paymentElement != null)
                    {
                        Console.WriteLine("  Ссылка на оплату найдена!");
                        break;
                    }

                    if (pageText.Contains("не ответила") || pageText.Contains("Другие предложения"))
                    {
                        Console.WriteLine($"  Страховая не ответила ({(int)(DateTime.Now - pollStart).TotalSeconds}с)");
                        break;
                    }
                }

                if (paymentElement != null) break;

                // Retry: выбираем следующий оффер из списка
                TakeScreenshot($"retry_{attempt + 1:D2}_offers");

                var offerBtns = _driver.FindElements(By.XPath("//button[normalize-space(.)='Оформить']"))
                    .Where(e => { try { return e.Displayed; } catch { return false; } })
                    .ToList();

                Console.WriteLine($"  Доступных офферов: {offerBtns.Count}");

                if (!offerBtns.Any())
                {
                    Console.WriteLine("  Нет доступных офферов для retry");
                    break;
                }

                var offerToClick = offerBtns[Math.Min(attempt, offerBtns.Count - 1)];
                Console.WriteLine($"  Нажимаем оффер #{Math.Min(attempt, offerBtns.Count - 1) + 1}");
                offerToClick.Click();
                Thread.Sleep(5000);
                TakeScreenshot($"retry_{attempt + 1:D2}_after_offer");

                // Если появилась страница проверки — нажимаем «Все верно»
                var verifyBtns = _driver.FindElements(By.XPath("//button[contains(normalize-space(.),'Все верно')]"))
                    .Where(e => { try { return e.Displayed; } catch { return false; } })
                    .ToList();
                if (verifyBtns.Any())
                {
                    Console.WriteLine("  Нажимаем 'Все верно'");
                    verifyBtns.First().Click();
                    Thread.Sleep(5000);
                    TakeScreenshot($"retry_{attempt + 1:D2}_after_verify");
                }
            }

            TakeScreenshot("21_final_state");

            Assert.That(paymentElement, Is.Not.Null, "Ссылка/кнопка оплаты не появилась ни у одной страховой");
            Console.WriteLine($"  Ссылка на оплату: {paymentElement!.GetAttribute("href") ?? paymentElement.Text}");

            // Черновик полиса на той же странице
            var draftElement = _driver.FindElements(By.XPath(
                    "//a[contains(normalize-space(.),'черновик') or contains(normalize-space(.),'Черновик') or " +
                    "contains(normalize-space(.),'Предварительный') or contains(@href,'.pdf')] | " +
                    "//button[contains(normalize-space(.),'черновик') or contains(normalize-space(.),'Черновик')]"))
                .FirstOrDefault(o => { try { return o.Displayed; } catch { return false; } });

            Assert.That(draftElement, Is.Not.Null, "Ссылка на черновик полиса не найдена");
            Console.WriteLine($"  Черновик полиса: {draftElement!.GetAttribute("href") ?? draftElement.Text}");

            TakeScreenshot("22_final");
        }
    }
}
