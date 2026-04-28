using Ardin.TelegramSummaryBot.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TelegramSummaryBot;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== BOT STARTED ===");

        var config = LoadConfiguration();

        // 1. چک کردن حالت لاگین دستی (Remote Debugging)
        if (args.Contains("--login"))
        {
            Console.WriteLine("\n[MANUAL LOGIN MODE ACTIVATED]");
            await RunManualLoginAsync(config);
            return; // خروج از برنامه تا وارد حلقه نشود
        }

        // 3. اجرای اصلی برنامه
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

    // متد اجرای حالت لاگین
    private static async Task RunManualLoginAsync(IConfiguration config)
    {
        try
        {
            var botOrchestrator = new BotOrchestrator(config);
            await botOrchestrator.ManualLoginAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"!!! ERROR during Login: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static async Task RunSchedulerAsync(IConfiguration config, bool forceRun)
    {
        Console.WriteLine($"Scheduler initialized. Force run flag: {forceRun}");
        DateTime? lastRunTime = null;

        while (true)
        {
            var iranTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Iran Standard Time");

            bool isTopOfHour = iranTime.Minute == 0;
            bool alreadyRunThisHour = lastRunTime.HasValue &&
                                      lastRunTime.Value.Date == iranTime.Date &&
                                      lastRunTime.Value.Hour == iranTime.Hour;

            if (forceRun || (isTopOfHour && !alreadyRunThisHour))
            {
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
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}