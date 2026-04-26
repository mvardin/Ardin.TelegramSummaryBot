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
                var aiSummaryService = new AiSummaryService(Tokens.AIKey);

                Console.WriteLine("[TTS] Optimizing text for speech...");
                var optimizedText = await aiSummaryService.OptimizeToTTS(text);

                Console.WriteLine($"[TTS] Optimized text length: {optimizedText.Length}");

                Console.WriteLine("[TTS] Converting text to WAV using Piper...");
                string wavPath = convertTextToSpeech(configuration, optimizedText);

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

        private string convertTextToSpeech(IConfiguration configuration, string text)
        {
            try
            {
                var piperRoot = configuration.GetValue<string>("Piper:Path");

                if (string.IsNullOrWhiteSpace(piperRoot))
                {
                    Console.WriteLine("[TTS] Piper path not configured.");
                    return string.Empty;
                }

                Console.WriteLine($"[TTS] Piper root: {piperRoot}");

                var wavPath = Path.Combine(piperRoot, "output", Guid.NewGuid() + ".wav");
                Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);

                var piperExe = Path.Combine(piperRoot, "piper");
                string modelPath = Path.Combine(piperRoot, "voices/fa/fa_IR-amir-medium.onnx");
                string espeakPath = Path.Combine(piperRoot, "espeak-ng-data");

                Console.WriteLine("[TTS] Piper executable: " + piperExe);
                Console.WriteLine("[TTS] Model: " + modelPath);

                ProcessStartInfo psi = new ProcessStartInfo()
                {
                    FileName = piperExe,
                    WorkingDirectory = piperRoot,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false,
                    UseShellExecute = false,
                    Arguments = $"-m \"{modelPath}\" --espeak_data \"{espeakPath}\" -f \"{wavPath}\""
                };

                var stopwatch = Stopwatch.StartNew();

                using (Process process = new Process())
                {
                    process.StartInfo = psi;

                    Console.WriteLine("[TTS] Starting Piper process...");
                    process.Start();

                    using (var writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
                    {
                        writer.Write(text);
                    }

                    string err = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    stopwatch.Stop();

                    Console.WriteLine($"[TTS] Piper finished in {stopwatch.ElapsedMilliseconds} ms");

                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine("[TTS] Piper failed.");
                        Console.WriteLine("[TTS] Piper error output: " + err);
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
    }
}
