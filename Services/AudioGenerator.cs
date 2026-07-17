using System;
using System.IO;

namespace Pomodoro.Services
{
    public static class AudioGenerator
    {
        private const int SampleRate = 22050; // Keep file size manageable

        public static string GetSoundPath(string soundName)
        {
            string appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PomodoroApp", "Sounds");
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            
            string fileName = soundName.ToLower().Replace(" ", "_") + ".wav";
            string filePath = Path.Combine(appFolder, fileName);

            // Re-generate if size is too small for 30s loop
            if (File.Exists(filePath))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    bool isLongSound = soundName == "White Noise" || soundName == "Brown Noise" || soundName == "Ticking Fast" || soundName == "Ticking Slow" || soundName == "Rain" || soundName == "Ocean Waves";
                    if (isLongSound && fileInfo.Length < 500000)
                    {
                        File.Delete(filePath);
                    }
                }
                catch { }
            }

            if (!File.Exists(filePath))
            {
                GenerateSoundFile(soundName, filePath);
            }

            return filePath;
        }

        private static void GenerateSoundFile(string soundName, string filePath)
        {
            try
            {
                switch (soundName)
                {
                    // Alarms
                    case "Bell":
                        GenerateBell(filePath);
                        break;
                    case "Bird":
                        GenerateBird(filePath);
                        break;
                    case "Digital":
                        GenerateDigital(filePath);
                        break;
                    case "Kitchen":
                        GenerateKitchen(filePath);
                        break;
                    case "Wood":
                        GenerateWood(filePath);
                        break;

                    // Focus Ambient Sounds
                    case "Ticking Fast":
                        GenerateTicking(filePath, 0.25);
                        break;
                    case "Ticking Slow":
                        GenerateTicking(filePath, 1.0);
                        break;
                    case "White Noise":
                        GenerateWhiteNoise(filePath);
                        break;
                    case "Brown Noise":
                        GenerateBrownNoise(filePath);
                        break;
                    case "Rain":
                        GenerateRain(filePath);
                        break;
                    case "Ocean Waves":
                        GenerateOcean(filePath);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to generate sound {soundName}: {ex.Message}");
            }
        }

        private static void WriteWavHeader(FileStream fs, int dataLength)
        {
            using (var bw = new BinaryWriter(fs, System.Text.Encoding.UTF8, true))
            {
                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataLength);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16); // subchunk1Size
                bw.Write((short)1); // AudioFormat (PCM = 1)
                bw.Write((short)1); // NumChannels (Mono = 1)
                bw.Write(SampleRate);
                bw.Write(SampleRate * 2); // ByteRate = SampleRate * NumChannels * BitsPerSample/8
                bw.Write((short)2); // BlockAlign = NumChannels * BitsPerSample/8
                bw.Write((short)16); // BitsPerSample = 16
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(dataLength);
            }
        }

        private static void ApplyFade(short[] samples, double fadeDurationSeconds)
        {
            int fadeLength = (int)(SampleRate * fadeDurationSeconds);
            int numSamples = samples.Length;

            for (int i = 0; i < fadeLength && i < numSamples; i++)
            {
                double multiplier = (double)i / fadeLength;
                samples[i] = (short)(samples[i] * multiplier);
            }

            for (int i = 0; i < fadeLength && i < numSamples; i++)
            {
                int idx = numSamples - 1 - i;
                double multiplier = (double)i / fadeLength;
                samples[idx] = (short)(samples[idx] * multiplier);
            }
        }

        #region Alarm Sound Generators (3-second WAVs)

        private static void GenerateBell(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            
            // Decaying sine wave at 520Hz + harmonics
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double decay = Math.Exp(-1.5 * t);
                double wave = Math.Sin(2 * Math.PI * 520 * t) + 
                              0.5 * Math.Sin(2 * Math.PI * 1040 * t) + 
                              0.25 * Math.Sin(2 * Math.PI * 1560 * t);
                samples[i] = (short)(wave / 1.75 * decay * short.MaxValue * 0.5);
            }

            SaveWavFile(filePath, samples);
        }

        private static void GenerateBird(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            
            // Chirping frequency sweep
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double cycle = t % 0.4;
                if (cycle < 0.25)
                {
                    // sweep from 1500Hz to 3000Hz
                    double freq = 1500.0 + 1500.0 * (cycle / 0.25);
                    double phase = 2 * Math.PI * freq * cycle;
                    double ampDecay = Math.Sin(Math.PI * (cycle / 0.25)); // fade in/out
                    samples[i] = (short)(Math.Sin(phase) * ampDecay * short.MaxValue * 0.4);
                }
                else
                {
                    samples[i] = 0;
                }
            }

            SaveWavFile(filePath, samples);
        }

        private static void GenerateDigital(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            
            // Retro synth beeps (square waves)
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double cycle = t % 0.5;
                if (cycle < 0.15)
                {
                    double wave = Math.Sin(2 * Math.PI * 880 * t) > 0 ? 1.0 : -1.0;
                    samples[i] = (short)(wave * short.MaxValue * 0.2);
                }
                else
                {
                    samples[i] = 0;
                }
            }

            SaveWavFile(filePath, samples);
        }

        private static void GenerateKitchen(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            
            // Mechanical kitchen bell ring (high pitch decaying wave)
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double cycle = t % 0.3;
                double decay = Math.Exp(-10 * cycle);
                double wave = Math.Sin(2 * Math.PI * 2500 * t);
                samples[i] = (short)(wave * decay * short.MaxValue * 0.35);
            }

            SaveWavFile(filePath, samples);
        }

        private static void GenerateWood(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            
            // Short hollow wood block sound (very fast decaying 150Hz sine wave)
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double cycle = t % 0.6;
                if (cycle < 0.1)
                {
                    double decay = Math.Exp(-40 * cycle);
                    double wave = Math.Sin(2 * Math.PI * 180 * t);
                    samples[i] = (short)(wave * decay * short.MaxValue * 0.5);
                }
                else
                {
                    samples[i] = 0;
                }
            }

            SaveWavFile(filePath, samples);
        }

        #endregion

        #region Focus Ambient Sound Generators (30-second WAVs)

        private static void GenerateTicking(string filePath, double intervalSeconds)
        {
            int durationSeconds = 30;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double cycle = t % intervalSeconds;
                
                // Very brief click sound at start of interval
                if (cycle < 0.015)
                {
                    double decay = Math.Exp(-200.0 * cycle);
                    double wave = Math.Sin(2 * Math.PI * 900.0 * cycle);
                    samples[i] = (short)(wave * decay * short.MaxValue * 0.3);
                }
                else
                {
                    samples[i] = 0;
                }
            }

            ApplyFade(samples, 1.0);
            SaveWavFile(filePath, samples);
        }

        private static void GenerateWhiteNoise(string filePath)
        {
            int durationSeconds = 30;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            var rand = new Random();

            for (int i = 0; i < numSamples; i++)
            {
                samples[i] = (short)((rand.NextDouble() * 2.0 - 1.0) * short.MaxValue * 0.08);
            }

            ApplyFade(samples, 1.0);
            SaveWavFile(filePath, samples);
        }

        private static void GenerateBrownNoise(string filePath)
        {
            int durationSeconds = 30;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            var rand = new Random();
            double accumulator = 0;

            for (int i = 0; i < numSamples; i++)
            {
                double white = rand.NextDouble() * 2.0 - 1.0;
                accumulator = 0.98 * accumulator + 0.02 * white;
                samples[i] = (short)(accumulator * short.MaxValue * 0.8);
            }

            ApplyFade(samples, 1.0);
            SaveWavFile(filePath, samples);
        }

        private static void GenerateRain(string filePath)
        {
            int durationSeconds = 30;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            var rand = new Random();
            double lastValue = 0;

            for (int i = 0; i < numSamples; i++)
            {
                double white = rand.NextDouble() * 2.0 - 1.0;
                lastValue = 0.85 * lastValue + 0.15 * white;
                
                double click = 0;
                if (rand.NextDouble() < 0.0005)
                {
                    click = (rand.NextDouble() * 2.0 - 1.0) * 0.4;
                }
                samples[i] = (short)((lastValue * 0.08 + click) * short.MaxValue * 0.5);
            }

            ApplyFade(samples, 1.0);
            SaveWavFile(filePath, samples);
        }

        private static void GenerateOcean(string filePath)
        {
            int durationSeconds = 30;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            var rand = new Random();
            double accumulator = 0;

            for (int i = 0; i < numSamples; i++)
            {
                double white = rand.NextDouble() * 2.0 - 1.0;
                accumulator = 0.98 * accumulator + 0.02 * white;
                
                double t = (double)i / SampleRate;
                double modulation = 0.5 + 0.5 * Math.Sin(2 * Math.PI * t / 8.0);
                
                samples[i] = (short)(accumulator * modulation * short.MaxValue * 0.4);
            }

            ApplyFade(samples, 1.0);
            SaveWavFile(filePath, samples);
        }

        #endregion

        private static void SaveWavFile(string filePath, short[] samples)
        {
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                WriteWavHeader(fs, samples.Length * 2);
                using (var bw = new BinaryWriter(fs))
                {
                    foreach (var s in samples)
                    {
                        bw.Write(s);
                    }
                }
            }
        }
    }
}
