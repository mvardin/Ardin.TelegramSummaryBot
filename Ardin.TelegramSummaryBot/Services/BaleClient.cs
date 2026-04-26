using System.Net.Http.Json;
using System.Text.Json;

public class BaleClient
{
    private readonly HttpClient _http = new();
    private readonly string _token;

    public BaleClient(string token)
    {
        _token = token;
    }

    // --------------------------------------------------------
    // Retry Helper
    // --------------------------------------------------------
    private async Task<T?> Retry<T>(Func<Task<T?>> action, string actionName)
    {
        int[] delays = { 5, 10, 20, 30, 60 }; // seconds

        for (int i = 0; i < delays.Length; i++)
        {
            try
            {
                var result = await action();
                if (result is not null)
                    return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{actionName}] Exception (Attempt {i + 1}): {ex.Message}");
            }

            Console.WriteLine($"[{actionName}] Failed Attempt {i + 1}. Retrying in {delays[i]} seconds...");
            await Task.Delay(delays[i] * 1000);
        }

        Console.WriteLine($"[{actionName}] All Retry Attempts Failed.");
        return default;
    }

    // --------------------------------------------------------
    // 1) Send text by chat ID  (returns bool)
    // --------------------------------------------------------
    public Task<bool?> Send(long chatId, string msg, string parseMode = "Markdown")
    {
        return Retry<bool?>(async () =>
        {
            var url = $"https://tapi.bale.ai/bot{_token}/sendMessage";

            var payload = new
            {
                chat_id = chatId,
                text = msg,
                parse_mode = parseMode,
                disable_web_page_preview = true
            };

            var response = await _http.PostAsJsonAsync(url, payload);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[SendToId] API Error: {await response.Content.ReadAsStringAsync()}");
                return null;
            }

            return true;
        }, "SendToId");
    }

    // --------------------------------------------------------
    // 2) Send text by channel username (returns message_id)
    // --------------------------------------------------------
    public Task<int?> Send(string channelUsername, string msg, string parseMode = "Markdown")
    {
        return Retry<int?>(async () =>
        {
            var url = $"https://tapi.bale.ai/bot{_token}/sendMessage";

            var payload = new
            {
                chat_id = channelUsername,
                text = msg,
                parse_mode = parseMode,
                disable_web_page_preview = true
            };

            var response = await _http.PostAsJsonAsync(url, payload);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[SendToUser] API Error: {await response.Content.ReadAsStringAsync()}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("result", out var resultElement) &&
                    resultElement.TryGetProperty("message_id", out var msgId))
                {
                    return msgId.GetInt32();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendToUser] JSON Parse Error: {ex.Message}");
            }

            return null;
        }, "SendToUser");
    }

    // --------------------------------------------------------
    // 3) Send voice reply (returns bool)
    // --------------------------------------------------------
    public Task<bool?> SendVoiceReply(string channelUsername, string voiceFilePath, int replyToMessageId)
    {
        return Retry<bool?>(async () =>
        {
            var url = $"https://tapi.bale.ai/bot{_token}/sendAudio";

            using var multipart = new MultipartFormDataContent();

            multipart.Add(new StringContent(channelUsername), "chat_id");
            multipart.Add(new StringContent(replyToMessageId.ToString()), "reply_to_message_id");

            var fileBytes = await File.ReadAllBytesAsync(voiceFilePath);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");

            multipart.Add(fileContent, "audio", Path.GetFileName($"اخبار صوتی {replyToMessageId}"));

            var response = await _http.PostAsync(url, multipart);

            if (response.IsSuccessStatusCode) return true;
            Console.WriteLine($"[SendVoice] API Error: {await response.Content.ReadAsStringAsync()}");
            return null;

        }, "SendVoice");
    }
}
