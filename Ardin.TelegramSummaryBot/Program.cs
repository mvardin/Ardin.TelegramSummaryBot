using Ardin.TelegramSummaryBot.Services;
using Microsoft.Extensions.Configuration;
using static System.Net.Mime.MediaTypeNames;

namespace TelegramSummaryBot;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== BOT STARTED ===");

        // 1. Load Configuration
        var config = LoadConfiguration();

        bool forceRun = args.Contains("--run");
        await RunSchedulerAsync(config, forceRun);
    }

    private static IConfiguration LoadConfiguration()
    {
        Console.WriteLine("Loading configuration...");
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
    }

    private static async Task RunSchedulerAsync(IConfiguration config, bool forceRun)
    {
        Console.WriteLine($"Scheduler initialized. Force run flag: {forceRun}");
        DateTime? lastRunTime = null;

        while (true)
        {
            var iranTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Iran Standard Time");

            // بررسی اینکه آیا سر ساعت هستیم (دقیقه ۰) و آیا در این ساعت قبلا اجرا شده‌ایم یا خیر
            bool isTopOfHour = iranTime.Minute == 0;
            bool alreadyRunThisHour = lastRunTime.HasValue &&
                                      lastRunTime.Value.Date == iranTime.Date &&
                                      lastRunTime.Value.Hour == iranTime.Hour;
            
            if (forceRun || (isTopOfHour && !alreadyRunThisHour))
            {
                // خاموش کردن فلگ اجرای اجباری تا فقط یک بار اجرا شود
                forceRun = false;
                lastRunTime = iranTime;

                Console.WriteLine($"\n=== Task STARTED at {iranTime:yyyy-MM-dd HH:mm:ss} ===");
                try
                {

                    var botOrchestrator = new BotOrchestrator(config);

                    await botOrchestrator.ExecuteCurrentHourTasksAsync(iranTime);

                    Console.WriteLine("=== Task COMPLETED Successfully ===");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"!!! ERROR in Task: {ex.Message}\n{ex.StackTrace}");
                }
            }

            // چک کردن زمان هر 10 ثانیه یکبار
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}
