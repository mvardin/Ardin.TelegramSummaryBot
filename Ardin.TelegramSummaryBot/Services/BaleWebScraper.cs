using Ardin.TelegramSummaryBot.Models.Ardin.TelegramSummaryBot.Models;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardin.TelegramSummaryBot.Services;

public class BaleWebScraper : IDisposable
{
    private readonly IConfiguration _config;
    private ChromeDriver _driver;

    public BaleWebScraper(IConfiguration config)
    {
        _config = config;
    }

    public void Initialize()
    {
        Console.WriteLine("Starting ChromeDriver...");
        _driver = new ChromeDriverManager(_config).GetChromeDriver();

        Console.WriteLine("Checking Login Status...");
        CheckLogin();
    }

    public async Task<List<NewsMessage>> ScrapeChannelsAsync()
    {
        var channels = _config.GetSection("Bale:Channels").Get<string[]>();
        Console.WriteLine($"Loaded {channels.Length} channels from config.");

        var allMessages = new List<NewsMessage>();

        foreach (var channel in channels)
        {
            var messages = await ScrapeSingleChannelAsync(channel);
            allMessages.AddRange(messages);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        return allMessages;
    }

    private async Task<List<NewsMessage>> ScrapeSingleChannelAsync(string channelId)
    {
        Console.WriteLine($"Opening channel: {channelId}");
        await _driver.Navigate().GoToUrlAsync($"https://web.bale.ai/chat?uid={channelId}");

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(20));

        Console.WriteLine("Waiting for message list element...");
        var messageListEl = wait.Until(d =>
        {
            try
            {
                var el = d.FindElement(By.Id("message_list_scroller_id"));
                return el.Displayed ? el : null;
            }
            catch { return null; }
        });

        ScrollToBottom();

        Console.WriteLine("Waiting for messages to load...");
        wait.Until(d =>
        {
            try { return messageListEl.FindElements(By.CssSelector("div.message-item")).Count > 0; }
            catch { return false; }
        });

        Console.WriteLine("Extracting HTML...");
        var html = messageListEl.GetAttribute("outerHTML");
        var extracted = HtmlNewsExtractor.BaleExtractMessages(html);

        Console.WriteLine($"Extracted {extracted.Count} messages from channel {channelId}");
        return extracted;
    }

    private void ScrollToBottom()
    {
        try
        {
            var btn = _driver.FindElement(By.CssSelector("div[data-sentry-element='Fab']"));
            btn.Click();
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
        catch
        {
            Console.WriteLine("FAB (Scroll to bottom) not found or not clickable. Skipping.");
        }
    }

    private void CheckLogin()
    {
        Console.WriteLine("Navigating to Bale...");
        _driver.Navigate().GoToUrl("https://web.bale.ai/");
        _driver.ExecuteScript("window.localStorage.setItem('app.web.language', 'fa');");
        _driver.Navigate().Refresh();

        // مکث اولیه برای لود شدن کامل ساختار صفحه
        Thread.Sleep(5000);
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));

        // بررسی لاگین بودن
        if (!_driver.PageSource.Contains("Login.tsx"))
        {
            Console.WriteLine("Already logged in.");
            return;
        }

        Console.WriteLine("Login required. Waiting for login button...");
        IJavaScriptExecutor js = (IJavaScriptExecutor)_driver; // تعریف اجراکننده جاوا اسکریپت

        try
        {
            var loginButton = wait.Until(d => d.FindElement(By.XPath("//button[contains(text(),'ورود')]")));

            // --- راه حل خطای Element Click Intercepted ---
            // 1. اول اسکرول روی دکمه
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", loginButton);
            Thread.Sleep(500); // نیم ثانیه صبر برای انیمیشن‌های احتمالی

            // 2. کلیک با استفاده از جاوا اسکریپت (دور زدن بنرهای مزاحم)
            js.ExecuteScript("arguments[0].click();", loginButton);
            Console.WriteLine("Clicked on login button via JS.");
        }
        catch (Exception ex) // تغییر به Exception کلی برای گرفتن خطاهای Intercepted و Timeout
        {
            var fileName = $"page_source_error_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            File.WriteAllText(fileName, _driver.PageSource);
            Console.WriteLine($"Error clicking login button. Page source saved: {fileName}");
            Console.WriteLine($"Error Details: {ex.Message}");
            throw;
        }

        // وارد کردن شماره تماس
        Console.WriteLine("Entering phone number...");
        var input = wait.Until(d => d.FindElement(By.CssSelector("input[aria-label='شماره همراه']")));
        input.Clear();
        input.SendKeys("09359147574"); // می‌توان این را هم از کانفیگ خواند

        // کلیک روی دکمه تایید و ادامه (اینجا هم از JS استفاده می‌کنیم تا خطای مشابه ندهد)
        Console.WriteLine("Clicking submit button...");
        var btnSubmit = wait.Until(d => d.FindElement(By.XPath("//button[contains(text(),'تایید و ادامه')]")));
        js.ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", btnSubmit);
        Thread.Sleep(500);
        js.ExecuteScript("arguments[0].click();", btnSubmit);

        // ⚠️ هشدار: این خط روی سرورهای لینوکسی در پس‌زمینه برنامه را قفل می‌کند!
        // فقط در صورتی استفاده کنید که برنامه را مستقیماً در ترمینال (مثل tmux) اجرا می‌کنید.
        Console.Write("Enter OTP: ");
        var otp = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(otp))
        {
            throw new Exception("OTP cannot be empty!");
        }

        // وارد کردن کد تایید
        Console.WriteLine("Submitting OTP...");
        var otpInput = wait.Until(d => d.FindElement(By.CssSelector("input[data-testid='otp-input']")));
        otpInput.Clear();
        otpInput.SendKeys(otp);

        Console.WriteLine("Login process completed.");

        // یک مکث کوتاه برای اینکه سایت لاگین را پردازش کند
        Thread.Sleep(3000);
    }
    public void Dispose()
    {
        _driver?.Quit();
        _driver?.Dispose();
        Console.WriteLine("ChromeDriver disposed.");
    }
}
