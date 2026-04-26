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
            var aiSummaryService = new AiSummaryService(Tokens.AIKey);

            var optimizedText = await aiSummaryService.OptimizeToTTS(text);

            string wavPath = convertTextToSpeech(configuration, optimizedText);

            var mp3Path = wavPath.Replace(".wav", ".mp3");
            string backgroundPath = "C:\\Workplace\\Ardin.TelegramSummaryBot\\ffmpeg\\background.mp3";
            mp3Path = await new AudioProcessor().AddBackgroundMusicAsync(wavPath, backgroundPath, mp3Path, 1);

            return mp3Path;
        }

        private string convertTextToSpeech(IConfiguration configuration, string text)
        {
            try
            {
                var piperRoot = configuration.GetValue<string>("Piper:Path");

                var wavPath = Path.Combine(piperRoot, "output", Guid.NewGuid() + ".wav");
                Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);

                var piperExe = Path.Combine(piperRoot, "piper");

                string modelPath = Path.Combine(piperRoot, "voices/fa/fa_IR-amir-medium.onnx");
                string espeakPath = Path.Combine(piperRoot, "espeak-ng-data");

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

                using (Process process = new Process())
                {
                    process.StartInfo = psi;
                    process.Start();

                    // Write text to Piper
                    using (var writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
                    {
                        writer.Write(text);
                    }
                    // هیچ Close دیگری لازم نیست

                    string err = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        throw new Exception("Piper error: " + err);
                }

                return wavPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine("TTS ERROR: " + ex);
            }

            return string.Empty;
        }

    }
}
