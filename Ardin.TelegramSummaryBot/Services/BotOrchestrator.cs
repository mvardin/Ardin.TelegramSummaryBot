using Ardin.TelegramSummaryBot.Models.Ardin.TelegramSummaryBot.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TelegramSummaryBot;

public class BotOrchestrator
{
    private readonly IConfiguration _config;

    public BotOrchestrator(IConfiguration config)
    {
        _config = config;
    }

    public async Task ExecuteWorkflowAsync()
    {
        List<NewsMessage> recentMessages;

        // 1. استخراج پیام‌ها از طریق سلنیوم
        using (var scraper = new BaleWebScraper(_config))
        {
            scraper.Initialize();
            var allMessages = await scraper.ScrapeChannelsAsync();
            recentMessages = FilterRecentMessages(allMessages, hours: 1);
        }

        Console.WriteLine($"Found {recentMessages.Count} recent messages in the last 1 hour.");

        if (!recentMessages.Any())
        {
            Console.WriteLine("No recent messages found. Skipping AI and Telegram steps.");
            return;
        }

        // 2. ارسال به هوش مصنوعی برای خلاصه‌سازی
        Console.WriteLine("Sending messages to AI for summarization...");
        var aiService = new AiSummaryService(Tokens.AIKey);
        var aiSummary = await aiService.GenerateNewsSummary(recentMessages);
        Console.WriteLine("AI summary received.");

        // 3. ارسال به کانال بله
        await SendToBaleChannelAsync(aiSummary);
    }

    private List<NewsMessage> FilterRecentMessages(List<NewsMessage> messages, int hours)
    {
        Console.WriteLine($"Filtering messages newer than {hours} hour(s)...");
        var timeLimit = DateTimeOffset.UtcNow.AddHours(-hours);

        return messages
            .Where(m => DateTimeOffset.FromUnixTimeMilliseconds(m.DateUnix) >= timeLimit)
            .ToList();
    }

    private async Task SendToBaleChannelAsync(string aiContent)
    {
        Console.WriteLine("Sending summary to Bale...");

        var baleMessage =
            $"🔴 سرتیتر مهم‌ترین اخبار یک ساعت اخیر\n\n" +
            $"{aiContent}\n\n" +
            $"📡 @ZTVOfficial";

        var baleClient = new BaleClient(Tokens.BaleToken);
        await baleClient.Send("@ztvofficial", baleMessage);

        Console.WriteLine("Message sent to Bale.");
    }
}

