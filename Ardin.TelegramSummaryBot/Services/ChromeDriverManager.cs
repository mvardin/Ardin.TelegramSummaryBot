using System;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Ardin.TelegramSummaryBot.Services
{
    public class ChromeDriverManager
    {
        private readonly IConfiguration _config;

        public ChromeDriverManager(IConfiguration config)
        {
            _config = config;
        }

        public ChromeDriver GetChromeDriver() // خروجی را به IWebDriver تغییر دادیم که استانداردتر است
        {
            var options = GetChromeOptions();
            var service = CreateChromeDriverService();

            return new ChromeDriver(service, options);
        }

        private ChromeOptions GetChromeOptions()
        {
            var userDataDir = _config["ChromeSettings:UserDataDir"];
            var options = new ChromeOptions();

            var chromeBinary = _config["ChromeSettings:ChromeDirectory"];
            if (!string.IsNullOrEmpty(chromeBinary))
            {
                options.BinaryLocation = chromeBinary;
            }

            options.AddArgument("--headless=new");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--remote-allow-origins=*");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--disable-notifications");
            options.AddArgument("--disable-infobars");

            if (!string.IsNullOrEmpty(userDataDir))
            {
                options.AddArgument($"--user-data-dir={userDataDir}");
            }

            options.AddArgument("--remote-debugging-port=9222");

            return options;
        }

        // نام این متد را تغییر دادیم تا با نام کلاس تداخل نداشته باشد
        private OpenQA.Selenium.Chrome.ChromeDriverService CreateChromeDriverService()
        {
            var driverPath = _config["ChromeSettings:ChromeDriverDirectory"];
            var service = OpenQA.Selenium.Chrome.ChromeDriverService.CreateDefaultService(driverPath);
            service.EnableVerboseLogging = true;
            return service;
        }
    }
}