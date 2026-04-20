using Ardin.TelegramSummaryBot.Models.Ardin.TelegramSummaryBot.Models;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;

public class AiSummaryService
{
    private readonly OpenAIClient _client;

    public AiSummaryService(string apiKey)
    {
        _client = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri("https://api.gapgpt.app/v1") });
    }

    public async Task<string> GenerateNewsSummary(List<NewsMessage> messages)
    {
        var builder = new StringBuilder();

        foreach (var m in messages)
        {
            if (!string.IsNullOrWhiteSpace(m.Text))
            {
                builder.AppendLine("- " + m.Text);
            }
        }

        var rawText = builder.ToString();

        if (string.IsNullOrWhiteSpace(rawText))
            return string.Empty;

        var prompt = $@"متن زیر مجموعه‌ای از خبرهای یک ساعت اخیر از چند کانال خبری است.

                    این خبرها را بررسی کن و فقط مهم‌ترین و معتبرترین موارد را استخراج کن.

                    سپس خروجی را به صورت «تیترهای خبری کوتاه و مستقل» بنویس؛
                    به شکل بولت‌پوینت (🔹).

                    شرایط:
                    - هر تیتر باید مستقل باشد و اخبار را با هم ادغام نکن.
                    - از هر خبر فقط نکته کلیدی آن را استخراج کن.
                    - تیترها باید کوتاه، جذاب، دقیق و کاملاً بی‌طرفانه باشند.
                    - سبک نوشتار شبیه «فید فوری خبری» باشد.
                    - از تحلیل، پیش‌بینی یا نظر شخصی خودداری کن.
                    - هیچ متن اضافی قبل یا بعد از تیترها ننویس.

                    متن خبرها:
                    {rawText}

                    لطفاً تیترهای نهایی را بنویس.
                    ";

        var chat = _client.GetChatClient("gpt-4o-mini");

        var chatMessages = new List<ChatMessage>
        {
            ChatMessage.CreateUserMessage(prompt)
        };

        var response = await chat.CompleteChatAsync(chatMessages);
        var summary = response?.Value.Content[0].Text.Trim();

        return summary ?? string.Empty;
    }
}