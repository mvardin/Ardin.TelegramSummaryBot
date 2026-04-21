using Ardin.TelegramSummaryBot.Models.Ardin.TelegramSummaryBot.Models;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;
using TelegramSummaryBot;

public class AiSummaryService
{
    private readonly OpenAIClient _client;
    private const string ModelName = "gpt-4o-mini"; // تعریف مدل در یک ثابت

    public AiSummaryService(string apiKey)
    {
        _client = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri("https://api.gapgpt.app/v1") });
    }

    public async Task<string> GenerateNewsSummary(List<NewsMessage> messages)
    {
        var rawText = BuildNewsText(messages);
        if (string.IsNullOrWhiteSpace(rawText))
            return string.Empty;

        string systemPrompt = @"شما یک دستیار هوشمند و بی‌طرف برای خلاصه‌سازی اخبار هستید.
خبرها را بررسی کن و فقط مهم‌ترین و معتبرترین موارد را استخراج کن.
خروجی را به صورت «تیترهای خبری کوتاه و مستقل» بنویس (به شکل بولت‌پوینت 🔹).

شرایط:
- هر تیتر باید مستقل باشد و اخبار را با هم ادغام نکن.
- از هر خبر فقط نکته کلیدی آن را استخراج کن.
- تیترها باید کوتاه، جذاب، دقیق و کاملاً بی‌طرفانه باشند.
- سبک نوشتار شبیه «فید فوری خبری» باشد.
- از تحلیل، پیش‌بینی یا نظر شخصی خودداری کن.
- هیچ متن اضافی قبل یا بعد از تیترها ننویس.";

        string userPrompt = $"متن خبرهای یک ساعت اخیر:\n{rawText}\n\nلطفاً تیترهای نهایی را بنویس.";

        return await ExecuteAiRequestAsync(systemPrompt, userPrompt);
    }

    public async Task<string> GenerateDeepAnalysis(List<NewsMessage> news, List<AnalysisRecord> previousAnalyses)
    {
        var rawText = BuildNewsText(news);
        if (string.IsNullOrWhiteSpace(rawText))
            return string.Empty;

        var contextBuilder = new StringBuilder();
        if (previousAnalyses.Any())
        {
            contextBuilder.AppendLine("سابقه تحلیل‌های قبلی (برای درک بهتر روند اخبار):");
            for (int i = 0; i < previousAnalyses.Count; i++)
            {
                contextBuilder.AppendLine($"--- تحلیل {i + 1} ({previousAnalyses[i].Date:yyyy-MM-dd HH:mm}) ---");
                contextBuilder.AppendLine(previousAnalyses[i].Content);
            }
            contextBuilder.AppendLine("=========================================");
        }

        string systemPrompt = @"شما یک تحلیلگر ارشد و استراتژیست اخبار هستید.
با استفاده از سابقه تحلیل‌های قبلی (در صورت وجود) که روند اتفاقات گذشته را نشان می‌دهد، اخبار جدید را تحلیل کنید.
سعی کنید ارتباط بین اخبار جدید و اتفاقات گذشته را درک کرده و یک تحلیل جامع، یکپارچه و بی‌طرفانه ارائه دهید.";

        string userPrompt = $"{contextBuilder}\nاخبار جدید برای تحلیل:\n{rawText}";

        return await ExecuteAiRequestAsync(systemPrompt, userPrompt);
    }

    // --- متدهای کمکی (Private Helpers) ---

    private async Task<string> ExecuteAiRequestAsync(string systemPrompt, string userPrompt)
    {
        var chat = _client.GetChatClient(ModelName);

        var chatMessages = new List<ChatMessage>
        {
            // استفاده از System Message برای دستورالعمل‌ها و نقش هوش مصنوعی
            ChatMessage.CreateSystemMessage(systemPrompt),
            // استفاده از User Message برای ارسال داده‌ها
            ChatMessage.CreateUserMessage(userPrompt)
        };

        var response = await chat.CompleteChatAsync(chatMessages);
        return response?.Value.Content[0].Text.Trim() ?? string.Empty;
    }

    private string BuildNewsText(List<NewsMessage> messages)
    {
        var builder = new StringBuilder();
        // فیلتر کردن خبرهای خالی با LINQ
        foreach (var m in messages.Where(m => !string.IsNullOrWhiteSpace(m.Text)))
        {
            builder.AppendLine($"- {m.Text}");
        }
        return builder.ToString();
    }
}
