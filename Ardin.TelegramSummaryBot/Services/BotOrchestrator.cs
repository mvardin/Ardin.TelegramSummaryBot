using Ardin.TelegramSummaryBot.Models.Ardin.TelegramSummaryBot.Models;
using Microsoft.Extensions.Configuration;
using TelegramSummaryBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        var aiService = new AiSummaryService(Tokens.AIKey);
        var aiSummary = await aiService.GenerateNewsSummary(recentMessages);

        await SendToBaleChannelAsync(aiSummary, isAnalysis: false);
    }

    private async Task ExecuteAnalysisWorkflowAsync(int hoursToLookBack)
    {
        var analysisNews = await _repository.GetNewsSinceAsync(hoursToLookBack);
        var previousAnalyses = await _repository.GetAnalysesAsync();

        Console.WriteLine($"Found {analysisNews.Count} saved messages and {previousAnalyses.Count} past analyses.");

        if (!analysisNews.Any()) return;

        var aiService = new AiSummaryService(Tokens.AIKey);
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

        var baleClient = new BaleClient(Tokens.BaleToken);
        await baleClient.Send("@zbriefnews", baleMessage);

        Console.WriteLine("Message sent to Bale.");
    }
}
