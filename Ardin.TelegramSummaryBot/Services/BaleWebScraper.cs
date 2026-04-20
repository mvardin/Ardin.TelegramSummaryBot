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
        var options = GetChromeOptions();
        var service = CreateChromeDriverService();

        Console.WriteLine("Starting ChromeDriver...");
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
            var messages = await ScrapeSingleChannelAsync(channel);
            allMessages.AddRange(messages);
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
        var extracted = HtmlNewsExtractor.ExtractMessages(html);

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

        Thread.Sleep(5000);
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));

        if (!_driver.PageSource.Contains("Login.tsx"))
        {
            Console.WriteLine("Already logged in.");
            return;
        }

        Console.WriteLine("Login required. Waiting for login button...");
        try
        {
            var loginButton = wait.Until(d => d.FindElement(By.XPath("//button[contains(text(),'ورود')]")));
            loginButton.Click();
        }
        catch (WebDriverTimeoutException)
        {
            var fileName = $"page_source_error_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            File.WriteAllText(fileName, _driver.PageSource);
            Console.WriteLine($"Login button not found. Page source saved: {fileName}");
            throw;
        }

        var input = wait.Until(d => d.FindElement(By.CssSelector("input[aria-label='شماره همراه']")));
        input.Clear();
        input.SendKeys("09359147574"); // می‌توان این را هم از کانفیگ خواند

        var btnSubmit = wait.Until(d => d.FindElement(By.XPath("//button[contains(text(),'تایید و ادامه')]")));
        btnSubmit.Click();

        Console.Write("Enter OTP: ");
        var otp = Console.ReadLine();

        Console.WriteLine("Submitting OTP...");
        var otpInput = wait.Until(d => d.FindElement(By.CssSelector("input[data-testid='otp-input']")));
        otpInput.Clear();
        otpInput.SendKeys(otp);

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
        options.AddArgument("--remote-debugging-port=9222");
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
