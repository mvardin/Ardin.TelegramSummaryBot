using System.Text.Json;
using Ardin.TelegramSummaryBot.Models.Ardin.TelegramSummaryBot.Models;

namespace TelegramSummaryBot;

public class LocalRepository
{
    private readonly string _dbFolder = "newsDB";
    private readonly string _newsFile;
    private readonly string _analysesFile;

    public LocalRepository()
    {
        _newsFile = Path.Combine(_dbFolder, "news.json");
        _analysesFile = Path.Combine(_dbFolder, "analyses.json");

        if (!Directory.Exists(_dbFolder))
            Directory.CreateDirectory(_dbFolder);
    }

    // --- بخش اخبار ---

    public async Task SaveAndCleanupNewsAsync(List<NewsMessage> newMessages)
    {
        var allNews = await GetAllNewsAsync();

        // اضافه کردن اخبار جدید و جلوگیری از تکرار بر اساس لینک یا ID پیام
        allNews.AddRange(newMessages);
        allNews = allNews.DistinctBy(m => m.Sid).ToList(); // فرض بر این است که MessageId یا Link یکتاست

        // حذف اخبار قدیمی‌تر از 24 ساعت
        var timeLimit = DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeMilliseconds();
        allNews = allNews.Where(m => m.DateUnix >= timeLimit).ToList();

        await File.WriteAllTextAsync(_newsFile, JsonSerializer.Serialize(allNews));
    }

    public async Task<List<NewsMessage>> GetNewsSinceAsync(int hoursToLookBack)
    {
        var allNews = await GetAllNewsAsync();
        var timeLimit = DateTimeOffset.UtcNow.AddHours(-hoursToLookBack).ToUnixTimeMilliseconds();

        return allNews.Where(m => m.DateUnix >= timeLimit).ToList();
    }

    private async Task<List<NewsMessage>> GetAllNewsAsync()
    {
        if (!File.Exists(_newsFile)) return new List<NewsMessage>();
        var json = await File.ReadAllTextAsync(_newsFile);
        return JsonSerializer.Deserialize<List<NewsMessage>>(json) ?? new List<NewsMessage>();
    }

    // --- بخش تحلیل‌ها ---

    public async Task SaveAndCleanupAnalysisAsync(string analysisContent)
    {
        var analyses = await GetAnalysesAsync();

        // اضافه کردن تحلیل جدید
        analyses.Add(new AnalysisRecord { Date = DateTime.UtcNow, Content = analysisContent });

        // نگه داشتن فقط 10 تحلیل آخر (مرتب‌سازی نزولی و انتخاب 10 تای اول، سپس برگرداندن به ترتیب صعودی)
        analyses = analyses.OrderByDescending(a => a.Date).Take(10).OrderBy(a => a.Date).ToList();

        await File.WriteAllTextAsync(_analysesFile, JsonSerializer.Serialize(analyses));
    }

    public async Task<List<AnalysisRecord>> GetAnalysesAsync()
    {
        if (!File.Exists(_analysesFile)) return new List<AnalysisRecord>();
        var json = await File.ReadAllTextAsync(_analysesFile);
        return JsonSerializer.Deserialize<List<AnalysisRecord>>(json) ?? new List<AnalysisRecord>();
    }
}

public class AnalysisRecord
{
    public DateTime Date { get; set; }
    public string Content { get; set; }
}
