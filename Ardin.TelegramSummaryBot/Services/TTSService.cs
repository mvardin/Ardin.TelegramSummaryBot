using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Ardin.TelegramSummaryBot.Services
{
    public class TTSService(IConfiguration configuration)
    {
        public async Task<string> Convert(string text)
        {
            Console.WriteLine("[TTS] Starting conversion...");

            try
            {
                var aiSummaryService = new AIService(Tokens.AIKey);

                Console.WriteLine("[TTS] Optimizing text for speech...");
                var optimizedText = await aiSummaryService.OptimizeToTTS(text);

                Console.WriteLine($"[TTS] Optimized text length: {optimizedText.Length}");

                Console.WriteLine("[TTS] Converting text to WAV using Piper...");
                string wavPath = await convertTextToSpeech(configuration, optimizedText);

                if (string.IsNullOrWhiteSpace(wavPath) || !File.Exists(wavPath))
                {
                    Console.WriteLine("[TTS] WAV generation failed.");
                    return string.Empty;
                }

                Console.WriteLine($"[TTS] WAV created: {wavPath}");

                var mp3Path = wavPath.Replace(".wav", ".mp3");

                string backgroundPath = "C:\\Workplace\\Ardin.TelegramSummaryBot\\ffmpeg\\background.mp3";

                Console.WriteLine("[TTS] Adding background music...");
                mp3Path = await new AudioProcessor().AddBackgroundMusicAsync(
                    wavPath,
                    backgroundPath,
                    mp3Path,
                    1);

                Console.WriteLine($"[TTS] Final MP3 ready: {mp3Path}");

                return mp3Path;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[TTS] Convert ERROR: " + ex);
                return string.Empty;
            }
        }

        private async Task<string> convertTextToSpeech(IConfiguration configuration, string text)
        {
            try
            {
                var piperRoot = configuration.GetValue<string>("Piper:Path");

                if (string.IsNullOrWhiteSpace(piperRoot))
                {
                    Console.WriteLine("[TTS] Piper path not configured.");
                    return string.Empty;
                }

                // پیش‌پردازش متن برای خوانش بهتر (اختیاری اما به شدت توصیه شده برای متون طولانی)
                text = PreprocessPersianText(text);

                var wavPath = Path.Combine(piperRoot, "output", Guid.NewGuid() + ".wav");
                Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);

                var piperExe = Path.Combine(piperRoot, "piper");
                string modelPath = Path.Combine(piperRoot, "voices/fa/fa_IR-amir-medium.onnx");
                string espeakPath = Path.Combine(piperRoot, "espeak-ng-data");

                // تنظیمات صدا (این مقادیر را تست کنید تا به بهترین حالت برسید)
                // --length_scale: سرعت خواندن (عدد بزرگتر = کندتر، مثلا 1.1 یا 1.2 برای اخبار مناسب‌تر است)
                // --noise_scale: میزان تنوع در تولید حروف صدادار (حدود 0.667 پیش‌فرض است)
                // --noise_w: تنوع در طول فونم‌ها (حدود 0.333 پیش‌فرض است)
                string speed = "1.1";
                string noiseScale = "0.667";
                string noiseW = "0.333";

                string arguments = $"-m \"{modelPath}\" --espeak_data \"{espeakPath}\" " +
                                   $"--length_scale {speed} --noise_scale {noiseScale} --noise_w {noiseW} -f \"{wavPath}\"";

                ProcessStartInfo psi = new ProcessStartInfo()
                {
                    FileName = piperExe,
                    WorkingDirectory = piperRoot,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = arguments
                };

                var stopwatch = Stopwatch.StartNew();

                using (Process process = new Process() { StartInfo = psi })
                {
                    Console.WriteLine("[TTS] Starting Piper process...");
                    process.Start();

                    // ارسال متن به صورت Stream با کدگذاری UTF-8
                    using (var writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
                    {
                        await writer.WriteAsync(text);
                    }

                    // خواندن خطاها به صورت غیرهمگام
                    string err = await process.StandardError.ReadToEndAsync();

                    await process.WaitForExitAsync();
                    stopwatch.Stop();

                    Console.WriteLine($"[TTS] Piper finished in {stopwatch.ElapsedMilliseconds} ms");

                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine("[TTS] Piper failed. Error: " + err);
                        throw new Exception("Piper error: " + err);
                    }
                }

                Console.WriteLine($"[TTS] WAV file generated: {wavPath}");
                return wavPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[TTS] convertTextToSpeech ERROR: " + ex);
                return string.Empty;
            }
        }
        private string PreprocessPersianText(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            // ۱. جایگزینی فاصله‌های اضافی قبل از علائم نگارشی تا مکث‌ها درست اعمال شوند
            input = input.Replace(" .", ".").Replace(" ،", "،").Replace(" ?", "؟").Replace(" !", "!");

            // ۲. برای متون خبری، Piper به علائم نگارشی (نقطه و ویرگول) برای نفس‌گیری نیاز دارد.
            // مطمئن شوید متن شما پاراگراف‌بندی و نقطه‌گذاری صحیحی دارد.
            // می‌توانید در صورت نیاز علائم خاصی که موتور اشتباه می‌خواند را اینجا حذف یا جایگزین کنید.

            return input;
        }
    }
}
