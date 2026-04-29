using Ardin.TelegramSummaryBot.Models.Ardin.TelegramSummaryBot.Models;
using Microsoft.Extensions.Configuration;
using TelegramSummaryBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardin.TelegramSummaryBot.Services;
using OpenQA.Selenium.Chrome;

public class BotOrchestrator
{
    private readonly IConfiguration _config;
    private readonly LocalRepository _repository;

    public BotOrchestrator(IConfiguration config)
    {
        _config = config;
        _repository = new LocalRepository();
    }

    public async Task ExecuteCurrentHourTasksAsync(DateTime currentTime)
    {
        Console.WriteLine("--- Executing Hourly News Workflow ---");
        await ExecuteHourlyWorkflowAsync();

        int[] analysisHours = [8, 14, 21];

        if (analysisHours.Contains(currentTime.Hour))
        {
            Console.WriteLine($"\n--- Analysis hour ({currentTime.Hour}) detected. Executing Analysis Workflow ---");

            int hoursToLookBack = 6;
            if (currentTime.Hour == 8) hoursToLookBack = 10;
            else if (currentTime.Hour == 14) hoursToLookBack = 6; // اصلاح کامنت شما: 8 تا 14 میشه 6 ساعت
            else if (currentTime.Hour == 21) hoursToLookBack = 7; // 14 تا 21 میشه 7 ساعت

            await ExecuteAnalysisWorkflowAsync(hoursToLookBack);
        }
    }
    public async Task ManualLoginAsync()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");

        // --- این دو خط جدید را اضافه کنید ---
        options.AddArgument("--remote-allow-origins=*");
        options.AddArgument("--remote-debugging-address=0.0.0.0");

        // 🔴 مسیر پوشه پروفایل (حتما مطمئن شوید این مسیر دسترسی نوشتن دارد)
        options.AddArgument("--user-data-dir=/home/workarea/chrome_profile");

        // 🔴 فعال‌سازی پورت دیباگ برای اتصال از ویندوز
        options.AddArgument("--remote-debugging-port=9222");

        Console.WriteLine("Starting Chrome in Remote Debugging mode...");
        using var driver = new ChromeDriver(options);

        // باز کردن سایت مورد نظر
        driver.Navigate().GoToUrl("https://web.bale.ai/");

        Console.WriteLine("\n=========================================================");
        Console.WriteLine("🌐 CHROME IS WAITING FOR MANUAL LOGIN...");
        Console.WriteLine("Please follow these steps on your WINDOWS machine:");
        Console.WriteLine("1. Open CMD or PowerShell.");
        Console.WriteLine("2. Run this exact command to create an SSH tunnel:");
        Console.WriteLine("   ssh -L 9222:localhost:9222 root@103.75.198.10");
        Console.WriteLine("3. Open Chrome on Windows and go to:");
        Console.WriteLine("   http://localhost:9222");
        Console.WriteLine("4. Click on 'Bale' link, and login using your mouse/keyboard.");
        Console.WriteLine("=========================================================");
        Console.WriteLine("\nPress [ENTER] here ONLY AFTER you have successfully logged in...");

        // برنامه اینجا متوقف می‌شود تا شما لاگین کنید و بعد اینتر بزنید
        Console.ReadLine();

        Console.WriteLine("Saving profile and closing browser...");
        driver.Quit();
        Console.WriteLine("✅ Profile saved successfully! Now you can run the bot normally.");
    }

    private async Task ExecuteHourlyWorkflowAsync()
    {
        var allMessages = new List<NewsMessage>();

        // 1. دریافت اخبار از بله
        Console.WriteLine("--- Scraping Bale ---");
        try
        {
            using (var baleScraper = new BaleWebScraper(_config))
            {
                baleScraper.Initialize();
                var baleMessages = await baleScraper.ScrapeChannelsAsync();
                allMessages.AddRange(baleMessages);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scraping Bale: {ex.Message}");
        }

        // 2. دریافت اخبار از تلگرام
        Console.WriteLine("--- Scraping Telegram ---");
        try
        {
            using (var tgScraper = new TelegramWebScraper(_config))
            {
                tgScraper.Initialize();
                var tgMessages = await tgScraper.ScrapeChannelsAsync();
                allMessages.AddRange(tgMessages);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scraping Telegram: {ex.Message}");
        }

        // 3. فیلتر کردن اخبار 1 ساعت اخیر
        var recentMessages = await FilterNewMessagesAsync(allMessages, hours: 1);

        // 4. ذخیره در دیتابیس محلی
        await _repository.SaveAndCleanupNewsAsync(recentMessages);

        if (!recentMessages.Any())
        {
            Console.WriteLine("No recent messages found to summarize.");
            return;
        }

        Console.WriteLine("Sending messages to AI for summarization...");
        var aiService = new AIService(_config);
        var aiSummary = await aiService.GenerateNewsSummary(recentMessages);

        await SendToBaleChannelAsync(aiSummary, isAnalysis: false);
    }
    private async Task ExecuteAnalysisWorkflowAsync(int hoursToLookBack)
    {
        var analysisNews = await _repository.GetNewsSinceAsync(hoursToLookBack);
        var previousAnalyses = await _repository.GetAnalysesAsync();

        Console.WriteLine($"Found {analysisNews.Count} saved messages and {previousAnalyses.Count} past analyses.");

        if (!analysisNews.Any()) return;

        var aiService = new AIService(_config);
        var aiAnalysis = await aiService.GenerateDeepAnalysis(analysisNews, previousAnalyses);

        await _repository.SaveAndCleanupAnalysisAsync(aiAnalysis);
        await SendToBaleChannelAsync(aiAnalysis, isAnalysis: true);
    }
    private async Task<List<NewsMessage>> FilterNewMessagesAsync(List<NewsMessage> messages, int hours)
    {
        Console.WriteLine($"Filtering new messages...");

        // دریافت اخبار قدیمی از دیتابیس (حتما باید await شود)
        // فرض بر این است که متد شما لیست برمی‌گرداند
        var oldNews = await _repository.GetAllNewsAsync();

        // استفاده از HashSet برای افزایش سرعت جستجو
        var existingSids = oldNews.Select(x => x.Sid).ToHashSet();
        var existingDates = oldNews.Select(x => x.DateUnix).ToHashSet();

        // فیلتر کردن پیام‌های جدید:
        // پیام‌هایی که Sid آن‌ها در دیتابیس نیست "و" DateUnix آن‌ها هم در دیتابیس نیست
        var newMessages = messages
            .Where(m => !existingSids.Contains(m.Sid) || !existingDates.Contains(m.DateUnix))
            .ToList();

        return newMessages;
    }
    private async Task SendToBaleChannelAsync(string aiContent, bool isAnalysis)
    {
        Console.WriteLine(isAnalysis ? "Sending ANALYSIS to Bale..." : "Sending HOURLY SUMMARY to Bale...");

        string header = isAnalysis
            ? "📊 تحلیل و جمع‌بندی اخبار ساعات گذشته\n\n"
            : "🔴 سرتیتر مهم‌ترین اخبار یک ساعت اخیر\n\n";

        var baleMessage =
            $"{header}" +
            $"{aiContent}\n\n" +
            $"📡 @ZBriefNews";

        var baleClient = new BaleClient(_config);
        var messageId = await baleClient.Send("@zbriefnews", baleMessage);
        if (messageId.HasValue)
        {
            TTSService ttsService = new TTSService(_config);
            string voicePath = await ttsService.Convert(baleMessage);
            await baleClient.SendVoiceReply("@zbriefnews", voicePath, messageId.Value);
        }

        Console.WriteLine("Message sent to Bale.");
    }
}
