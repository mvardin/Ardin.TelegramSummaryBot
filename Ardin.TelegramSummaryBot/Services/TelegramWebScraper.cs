using Ardin.TelegramSummaryBot.Models.Ardin.TelegramSummaryBot.Models;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class TelegramWebScraper : IDisposable
{
    private readonly IConfiguration _config;
    private ChromeDriver _driver;

    public TelegramWebScraper(IConfiguration config)
    {
        _config = config;
    }

    public void Initialize()
    {
        var options = GetChromeOptions();
        var service = CreateChromeDriverService();

        Console.WriteLine("Starting ChromeDriver for Telegram...");
        _driver = new ChromeDriver(service, options);

        Console.WriteLine("Checking Login Status...");
        CheckLogin();
    }

    public async Task<List<NewsMessage>> ScrapeChannelsAsync()
    {
        var channels = _config.GetSection("Telegram:Channels").Get<string[]>();
        Console.WriteLine($"Loaded {channels.Length} channels from config.");

        var allMessages = new List<NewsMessage>();

        foreach (var channel in channels)
        {
            var messages = await ScrapeSingleChannelByNameAsync(channel);
            allMessages.AddRange(messages);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        return allMessages;
    }

    private async Task<List<NewsMessage>> ScrapeSingleChannelByNameAsync(string channelName)
    {
        try
        {
            Console.WriteLine($"Opening Telegram Web...");
            string baseUrl = "https://web.telegram.org/a/";

            await _driver.Navigate().GoToUrlAsync(baseUrl);

            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(20));

            // 1. پیدا کردن بخش جستجو (search box)
            Console.WriteLine("Waiting for search input...");
            var searchInput = wait.Until(d =>
            {
                try
                {
                    var el = d.FindElement(By.CssSelector("input[placeholder='Search']"));
                    return el.Displayed ? el : null;
                }
                catch { return null; }
            });

            // 2. تایپ اسم کانال داخل بخش جستجو
            Console.WriteLine($"Searching for channel: {channelName}");
            searchInput.Clear();
            searchInput.SendKeys(channelName);
            Thread.Sleep(5000);
            searchInput.Clear();
            Thread.Sleep(1000);
            searchInput.SendKeys(channelName);
            Thread.Sleep(1000);

            // 3. منتظر نتایج جستجو بمانیم
            Console.WriteLine("Waiting for search results...");
            var searchResults = wait.Until(d =>
            {
                try
                {
                    // گرفتن همه المان‌های لیست نتایج جستجو: div با کلاس‌های search-result و chat-item-clickable
                    var list = d.FindElements(By.CssSelector("div.search-result.chat-item-clickable"));
                    return list.Count > 0 ? list : null;
                }
                catch { return null; }
            });

            var targetChannel = searchResults.FirstOrDefault(el =>
            {
                try
                {
                    // یافتن تگ h3 با کلاس fullName داخل هر آیتم برای بررسی نام کانال
                    var titleEl = el.FindElement(By.CssSelector("h3.fullName"));
                    if (titleEl != null)
                        return true;
                    return false;
                }
                catch
                {
                    return false;
                }
            });

            if (targetChannel == null)
            {
            }
            else
            {
                // کلیک روی کانال هدف
                targetChannel.Click();
            }

            // 5. منتظر بارگذاری پیام‌ها داخل کانال بمانیم
            Console.WriteLine("Waiting for messages to load...");
            var messageListEl = wait.Until(d =>
            {
                try
                {
                    var el = d.FindElement(By.CssSelector("div.MessageList"));
                    return el.Displayed ? el : null;
                }
                catch { return null; }
            });

            ScrollToBottom();

            wait.Until(d =>
            {
                try { return messageListEl.FindElements(By.CssSelector("div.Message")).Count > 0; }
                catch { return false; }
            });

            Console.WriteLine("Extracting HTML...");
            var html = messageListEl.GetAttribute("outerHTML");

            var extracted = HtmlNewsExtractor.TelegramExtractMessages(html);

            Console.WriteLine($"Extracted {extracted.Count} messages from channel {channelName}");
            return extracted;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error for {channelName}, {ex.Message}");
            return [];
        }
    }

    private void ScrollToBottom()
    {
        try
        {
            // دکمه اسکرول به پایین در تلگرام معمولاً در یک فلوتینگ باتن قرار دارد
            var btn = _driver.FindElement(By.CssSelector("button[aria-label='Go to bottom']"));
            if (btn.Displayed)
            {
                btn.Click();
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
        }
        catch
        {
            // اگر دکمه نبود، از طریق جاوا اسکریپت اسکرول می‌کنیم
            try
            {
                _driver.ExecuteScript("var list = document.querySelector('.MessageList'); if(list) list.scrollTop = list.scrollHeight;");
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
            catch
            {
                Console.WriteLine("Scroll to bottom failed or already at bottom. Skipping.");
            }
        }
    }

    private void CheckLogin()
    {
        Console.WriteLine("Navigating to Telegram Web...");
        _driver.Navigate().GoToUrl("https://web.telegram.org/a/");

        Thread.Sleep(5000); // صبر برای لود اولیه SPA تلگرام
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));

        // بررسی اینکه آیا به صفحه لاگین ریدایرکت شده‌ایم یا خیر
        // تلگرام معمولا وقتی لاگین نیستید آیدی auth-pages یا دکمه Log in by phone number را نمایش می‌دهد
        bool isLoggedIn = true;
        try
        {
            _driver.FindElement(By.Id("auth-pages"));
            isLoggedIn = false;
        }
        catch (NoSuchElementException)
        {
            isLoggedIn = true;
        }

        if (isLoggedIn)
        {
            Console.WriteLine("Already logged in (Session loaded via Profile).");
            return;
        }

        Console.WriteLine("Login required. Initiating login process...");
        try
        {
            // کلیک روی "ورود با شماره تلفن"
            var loginByPhoneBtn = wait.Until(d => d.FindElement(By.XPath("//button[contains(text(),'Log in by phone Number')]")));
            loginByPhoneBtn.Click();
        }
        catch (WebDriverTimeoutException)
        {
            var fileName = $"tg_page_source_error_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            File.WriteAllText(fileName, _driver.PageSource);
            Console.WriteLine($"Login button not found. Page source saved: {fileName}");
            throw;
        }

        // وارد کردن شماره تلفن (میتواند از کانفیگ خوانده شود)
        var phoneInput = wait.Until(d => d.FindElement(By.CssSelector("input[aria-label='Your phone number']")));
        // دقت کنید که پیش‌شماره (مثلا +98) در یک فیلد جداگانه است یا باید با شماره در همین فیلد وارد شود
        // تلگرام Web A معمولاً این را در یک اینپوت می‌گیرد:
        phoneInput.SendKeys("989359147574" + Keys.Enter);

        Console.Write("Enter OTP (Check your Telegram app on another device): ");
        var otp = Console.ReadLine();

        Console.WriteLine("Submitting OTP...");
        // اینپوت دریافت کد تلگرام معمولا به صورت مجزا برای هر رقم یا یک فیلد مخفی است
        var otpInput = wait.Until(d => d.FindElement(By.CssSelector("input[type='tel']")));
        otpInput.SendKeys(otp);

        Thread.Sleep(5000); // صبر برای تکمیل لاگین و لود شدن صفحه چت‌ها
        Console.WriteLine("Login process completed.");
    }

    private ChromeOptions GetChromeOptions()
    {
        var userDataDir = _config["ChromeSettings:UserDataDir"];
        var profileDirectory = _config["ChromeSettings:ProfileDirectory"];

        var options = new ChromeOptions
        {
            BinaryLocation = _config["ChromeSettings:ChromeDirectory"]
        };

        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--remote-debugging-port=9223");
        options.AddArgument($"--user-data-dir={userDataDir}");
        options.AddArgument($"--profile-directory={profileDirectory}");

        return options;
    }

    private ChromeDriverService CreateChromeDriverService()
    {
        var service = ChromeDriverService.CreateDefaultService(_config["ChromeSettings:ChromeDriverDirectory"]);
        service.EnableVerboseLogging = true;
        return service;
    }

    public void Dispose()
    {
        _driver?.Quit();
        _driver?.Dispose();
        Console.WriteLine("ChromeDriver disposed.");
    }
}
