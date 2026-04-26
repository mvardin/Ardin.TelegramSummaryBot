using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public class AudioProcessor
{
    // مسیر دقیق فایل اجرایی ffmpeg
    private readonly string _ffmpegPath = @"C:\Workplace\Ardin.TelegramSummaryBot\ffmpeg\ffmpeg.exe";

    /// <summary>
    /// متدی برای قرار دادن آهنگ پس‌زمینه روی فایل صوتی اصلی
    /// </summary>
    /// <param name="inputWavPath">مسیر فایل wav اصلی (صدای گوینده)</param>
    /// <param name="backgroundMp3Path">مسیر فایل mp3 پس‌زمینه</param>
    /// <param name="outputFilePath">مسیر ذخیره فایل نهایی</param>
    /// <param name="bgVolume">میزان بلندی صدای پس‌زمینه (مثلا 0.1 یعنی 10 درصد)</param>
    /// <returns>مسیر فایل خروجی</returns>
    public async Task<string> AddBackgroundMusicAsync(string inputWavPath, string backgroundMp3Path, string outputFilePath, double bgVolume = 0.15)
    {
        // بررسی وجود فایل‌های ورودی
        if (!File.Exists(inputWavPath))
            throw new FileNotFoundException($"فایل اصلی پیدا نشد: {inputWavPath}");

        if (!File.Exists(backgroundMp3Path))
            throw new FileNotFoundException($"آهنگ پس‌زمینه پیدا نشد: {backgroundMp3Path}");

        if (!File.Exists(_ffmpegPath))
            throw new FileNotFoundException($"فایل اجرایی FFmpeg در مسیر مشخص شده پیدا نشد: {_ffmpegPath}");

        // ساختار دستور FFmpeg:
        // -y : در صورت وجود فایل خروجی، آن را بدون پرسش بازنویسی می‌کند
        // [1:a]volume=... : صدای فایل دوم (پس‌زمینه) را کم می‌کند
        // amix=inputs=2:duration=first : دو صدا را ترکیب می‌کند و طول فایل نهایی را برابر با فایل اول در نظر می‌گیرد
        string arguments = $"-y -i \"{inputWavPath}\" -i \"{backgroundMp3Path}\" -filter_complex \"[1:a]volume={bgVolume}[bg];[0:a][bg]amix=inputs=2:duration=first:dropout_transition=2\" \"{outputFilePath}\"";

        var processStartInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true, // FFmpeg لاگ‌های خود را در Error Stream می‌نویسد
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = processStartInfo })
        {
            process.Start();

            // خواندن لاگ‌ها برای جلوگیری از قفل شدن پروسه (Deadlock)
            string errorOutput = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            // بررسی می‌کنیم که آیا عملیات موفقیت‌آمیز بوده یا خیر (کد صفر یعنی موفق)
            if (process.ExitCode != 0)
            {
                throw new Exception($"خطا در اجرای FFmpeg:\n{errorOutput}");
            }
        }

        // بررسی اینکه آیا فایل خروجی واقعا ساخته شده است
        if (File.Exists(outputFilePath))
        {
            return outputFilePath;
        }
        else
        {
            throw new Exception("عملیات به پایان رسید اما فایل خروجی ایجاد نشد.");
        }
    }
}