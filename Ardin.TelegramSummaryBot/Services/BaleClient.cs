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

    public async Task<bool> Send(long chatId, string msg, string parseMode = "Markdown")
    {
        var url = $"https://tapi.bale.ai/bot{_token}/sendMessage";

        var payload = new
        {
            chat_id = chatId,          // می‌تواند ID کانال باشد
            text = msg,
            parse_mode = parseMode,     // Markdown یا HTML
            disable_web_page_preview = true
        };

        var response = await _http.PostAsJsonAsync(url, payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Bale API Error: " + error);
            return false;
        }

        return true;
    }

    // نسخه‌ای که از username هم پشتیبانی می‌کند (مثلاً "@ZedTV")
    public async Task<bool> Send(string channelUsername, string msg, string parseMode = "Markdown")
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
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Bale API Error: " + error);
            return false;
        }

        return true;
    }
}