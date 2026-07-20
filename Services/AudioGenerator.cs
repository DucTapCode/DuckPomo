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

            string fileName = soundName.ToLower().Replace(" ", "_").Replace("_(đeo_tai_nghe)", "").Replace("(", "").Replace(")", "") + ".wav";
            string filePath = Path.Combine(appFolder, fileName);

            // Force re-generation for seamless loop upgrade
            string upgradeFlagPath = Path.Combine(appFolder, ".seamless_v2");
            if (!File.Exists(upgradeFlagPath))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(appFolder, "*.wav"))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { }
                    }
                    File.WriteAllText(upgradeFlagPath, "upgraded");
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
                    case "Tibetan Bowl":
                        GenerateTibetanBowl(filePath);
                        break;
                    case "Marimba":
                        GenerateMarimba(filePath);
                        break;
                    case "Gentle Chime":
                        GenerateGentleChime(filePath);
                        break;

                    // Focus Ambient Sounds (30s loops)
                    case "White Noise":
                        GenerateWhiteNoise(filePath);
                        break;
                    case "Pink Noise":
                        GeneratePinkNoise(filePath);
                        break;
                    case "Brown Noise":
                        GenerateBrownNoise(filePath);
                        break;
                    case "Ticking Fast":
                        GenerateTicking(filePath, 0.25);
                        break;
                    case "Ticking Slow":
                        GenerateTicking(filePath, 1.0);
                        break;
                    case "Rain":
                        GenerateRain(filePath);
                        break;
                    case "Ocean Waves":
                        GenerateOcean(filePath);
                        break;
                    case "Cozy Fireplace":
                        GenerateCozyFireplace(filePath);
                        break;
                    case "Coffee Shop":
                        GenerateCoffeeShop(filePath);
                        break;
                    case "Binaural Alpha (Đeo tai nghe)":
                        GenerateBinauralAlpha(filePath);
                        break;
                    case "Soft Wind":
                        GenerateSoftWind(filePath);
                        break;
                    case "Night Cricket":
                        GenerateNightCricket(filePath);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to generate sound {soundName}: {ex.Message}");
            }
        }

        private static void WriteWavHeader(FileStream fs, int dataLength, int numChannels)
        {
            using (var bw = new BinaryWriter(fs, System.Text.Encoding.UTF8, true))
            {
                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataLength);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16); // subchunk1Size
                bw.Write((short)1); // PCM = 1
                bw.Write((short)numChannels);
                bw.Write(SampleRate);
                bw.Write(SampleRate * numChannels * 2); // ByteRate
                bw.Write((short)(numChannels * 2)); // BlockAlign = numChannels * 2 (16-bit)
                bw.Write((short)16); // BitsPerSample = 16
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(dataLength);
            }
        }

        private static void SaveWavFile(string filePath, short[] samples, int numChannels)
        {
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                WriteWavHeader(fs, samples.Length * 2, numChannels);
                using (var bw = new BinaryWriter(fs))
                {
                    foreach (var s in samples)
                    {
                        bw.Write(s);
                    }
                }
            }
        }

        // Helper to generate a seamless looping stereo/mono buffer with dynamic sample logic
        private static short[] CreateSeamlessBuffer(int mainDurationSeconds, double crossfadeSeconds, int numChannels, Func<int, double, int, double> sampleGenerator)
        {
            int mainFrames = SampleRate * mainDurationSeconds;
            int crossfadeFrames = (int)(SampleRate * crossfadeSeconds);
            int totalFrames = mainFrames + crossfadeFrames;

            double[,] temp = new double[numChannels, totalFrames];
            for (int i = 0; i < totalFrames; i++)
            {
                double t = (double)i / SampleRate;
                for (int ch = 0; ch < numChannels; ch++)
                {
                    temp[ch, i] = sampleGenerator(i, t, ch);
                }
            }

            // Crossfade tail back into the head
            for (int i = 0; i < crossfadeFrames; i++)
            {
                double alpha = (double)i / crossfadeFrames;
                for (int ch = 0; ch < numChannels; ch++)
                {
                    int headIdx = i;
                    int tailIdx = mainFrames + i;
                    temp[ch, headIdx] = temp[ch, headIdx] * alpha + temp[ch, tailIdx] * (1.0 - alpha);
                }
            }

            // Short 5ms fade at boundaries to prevent click
            int boundaryFade = (int)(SampleRate * 0.005);
            for (int i = 0; i < boundaryFade; i++)
            {
                double alpha = (double)i / boundaryFade;
                for (int ch = 0; ch < numChannels; ch++)
                {
                    temp[ch, i] *= alpha;
                    temp[ch, mainFrames - 1 - i] *= alpha;
                }
            }

            // Flatten to 16-bit PCM WAV format
            short[] result = new short[mainFrames * numChannels];
            for (int i = 0; i < mainFrames; i++)
            {
                for (int ch = 0; ch < numChannels; ch++)
                {
                    // Soft clipping using Math.Tanh to prevent volume distortion
                    double limited = Math.Tanh(temp[ch, i]);
                    result[i * numChannels + ch] = (short)(limited * short.MaxValue);
                }
            }
            return result;
        }

        // Apply a quick 10ms fade to alarm sounds to prevent popping/clicking on play/stop
        private static void ApplyAlarmEnvelopes(short[] samples, double totalDuration, double fadeDuration = 0.01)
        {
            int fadeSamples = (int)(SampleRate * fadeDuration);
            for (int i = 0; i < fadeSamples && i < samples.Length; i++)
            {
                double alpha = (double)i / fadeSamples;
                samples[i] = (short)(samples[i] * alpha);
                int endIdx = samples.Length - 1 - i;
                samples[endIdx] = (short)(samples[endIdx] * alpha);
            }
        }

        #region Biquad DSP Filter Implementation

        public class BiquadFilter
        {
            private double a0, a1, a2, b0, b1, b2;
            private double x1, x2, y1, y2;

            public void SetLowPass(double cutoffFreq, double q = 0.707)
            {
                double omega = 2.0 * Math.PI * cutoffFreq / SampleRate;
                double alpha = Math.Sin(omega) / (2.0 * q);
                double cosOmega = Math.Cos(omega);

                b0 = (1.0 - cosOmega) / 2.0;
                b1 = 1.0 - cosOmega;
                b2 = (1.0 - cosOmega) / 2.0;
                a0 = 1.0 + alpha;
                a1 = -2.0 * cosOmega;
                a2 = 1.0 - alpha;
            }

            public void SetHighPass(double cutoffFreq, double q = 0.707)
            {
                double omega = 2.0 * Math.PI * cutoffFreq / SampleRate;
                double alpha = Math.Sin(omega) / (2.0 * q);
                double cosOmega = Math.Cos(omega);

                b0 = (1.0 + cosOmega) / 2.0;
                b1 = -(1.0 + cosOmega);
                b2 = (1.0 + cosOmega) / 2.0;
                a0 = 1.0 + alpha;
                a1 = -2.0 * cosOmega;
                a2 = 1.0 - alpha;
            }

            public void SetBandPass(double centerFreq, double q = 1.0)
            {
                double omega = 2.0 * Math.PI * centerFreq / SampleRate;
                double alpha = Math.Sin(omega) / (2.0 * q);
                double cosOmega = Math.Cos(omega);

                b0 = alpha;
                b1 = 0.0;
                b2 = -alpha;
                a0 = 1.0 + alpha;
                a1 = -2.0 * cosOmega;
                a2 = 1.0 - alpha;
            }

            public double Process(double sample)
            {
                double result = (b0 / a0) * sample + (b1 / a0) * x1 + (b2 / a0) * x2 - (a1 / a0) * y1 - (a2 / a0) * y2;
                x2 = x1;
                x1 = sample;
                y2 = y1;
                y1 = result;
                return result;
            }
        }

        #endregion

        #region Alarm Sound Generators (3-second WAVs, Mono)

        private static void GenerateBell(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];

            // Inharmonic frequencies + Tremolo (5Hz)
            double[] ratios = { 1.0, 2.76, 5.4, 8.9 };
            double[] weights = { 0.5, 0.25, 0.15, 0.1 };
            double baseFreq = 520.0;

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double decay = Math.Exp(-1.5 * t);
                double tremolo = 1.0 + 0.3 * Math.Sin(2 * Math.PI * 5 * t);
                
                double val = 0;
                for (int h = 0; h < ratios.Length; h++)
                {
                    val += Math.Sin(2 * Math.PI * baseFreq * ratios[h] * t) * weights[h];
                }
                
                double raw = val * tremolo * decay * 0.7;
                samples[i] = (short)(Math.Tanh(raw) * short.MaxValue);
            }

            ApplyAlarmEnvelopes(samples, durationSeconds);
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateBird(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];

            // Exponential Chirp + FM Modulator
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double cycle = t % 0.4;
                if (cycle < 0.25)
                {
                    // Exponential frequency sweep
                    double ratio = cycle / 0.25;
                    double freq = 1500.0 * Math.Pow(2.2, ratio);
                    
                    // FM Modulator at 40Hz
                    double fm = 150.0 * Math.Sin(2 * Math.PI * 40 * cycle);
                    double phase = 2 * Math.PI * (freq + fm) * cycle;

                    // Bell envelope for chirping amplitude
                    double env = Math.Sin(Math.PI * ratio);
                    double raw = Math.Sin(phase) * env * 0.45;
                    samples[i] = (short)(Math.Tanh(raw) * short.MaxValue);
                }
                else
                {
                    samples[i] = 0;
                }
            }

            ApplyAlarmEnvelopes(samples, durationSeconds);
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateDigital(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            
            var filter = new BiquadFilter();
            filter.SetLowPass(2000.0); // Cut off high frequency square wave buzz

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double cycle = t % 0.5;
                if (cycle < 0.15)
                {
                    double square = Math.Sin(2 * Math.PI * 880 * t) >= 0 ? 0.3 : -0.3;
                    double raw = filter.Process(square);
                    samples[i] = (short)(Math.Tanh(raw) * short.MaxValue);
                }
                else
                {
                    filter.Process(0); // Feed zero to filter state to decay naturally
                    samples[i] = 0;
                }
            }

            ApplyAlarmEnvelopes(samples, durationSeconds);
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateKitchen(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double cycle = t % 0.25;
                double decay = Math.Exp(-12.0 * cycle);
                double wave = Math.Sin(2 * Math.PI * 2200 * t);
                double raw = wave * decay * 0.5;
                samples[i] = (short)(Math.Tanh(raw) * short.MaxValue);
            }

            ApplyAlarmEnvelopes(samples, durationSeconds);
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateWood(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            var rand = new Random();

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double cycle = t % 0.6;
                if (cycle < 0.08)
                {
                    double decay = Math.Exp(-50.0 * cycle);
                    
                    // Inharmonic sine mixture
                    double wave = Math.Sin(2 * Math.PI * 300 * t) +
                                  0.6 * Math.Sin(2 * Math.PI * 470 * t) +
                                  0.3 * Math.Sin(2 * Math.PI * 820 * t);
                    
                    // 5ms Noise burst at transient start
                    double noise = 0;
                    if (cycle < 0.005)
                    {
                        noise = (rand.NextDouble() * 2.0 - 1.0) * 0.8 * (1.0 - cycle / 0.005);
                    }

                    double raw = (wave * 0.6 + noise * 0.4) * decay * 0.6;
                    samples[i] = (short)(Math.Tanh(raw) * short.MaxValue);
                }
                else
                {
                    samples[i] = 0;
                }
            }

            ApplyAlarmEnvelopes(samples, durationSeconds);
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateTibetanBowl(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];

            // Harmonic combination + phasing
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double decay = Math.Exp(-0.5 * t);
                
                // Phasing frequency beating: 216Hz and 218Hz
                double f1 = Math.Sin(2 * Math.PI * 216 * t);
                double f2 = Math.Sin(2 * Math.PI * 218 * t);
                
                // Soft harmonics
                double h1 = Math.Sin(2 * Math.PI * 432 * t) * 0.15;
                double h2 = Math.Sin(2 * Math.PI * 648 * t) * 0.07;
                
                double raw = (f1 * 0.4 + f2 * 0.4 + h1 + h2) * decay * 0.7;
                samples[i] = (short)(Math.Tanh(raw) * short.MaxValue);
            }

            ApplyAlarmEnvelopes(samples, durationSeconds);
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateMarimba(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];
            
            var filter = new BiquadFilter();
            filter.SetLowPass(1200.0, 1.2); // Warm wooden filter

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double cycle = t % 0.8;
                if (cycle < 0.25)
                {
                    double decay = Math.Exp(-25.0 * cycle);
                    double sine = Math.Sin(2 * Math.PI * 261.63 * t); // Middle C
                    double raw = filter.Process(sine * decay * 0.7);
                    samples[i] = (short)(Math.Tanh(raw) * short.MaxValue);
                }
                else
                {
                    filter.Process(0);
                    samples[i] = 0;
                }
            }

            ApplyAlarmEnvelopes(samples, durationSeconds);
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateGentleChime(string filePath)
        {
            int durationSeconds = 3;
            int numSamples = SampleRate * durationSeconds;
            short[] samples = new short[numSamples];

            // 3 notes offset by 0.15s
            double[] noteFreqs = { 1318.51, 1661.22, 1975.53 }; // E6, G#6, B6
            double[] startOffsets = { 0.0, 0.15, 0.3 };

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / SampleRate;
                double wave = 0;

                for (int note = 0; note < 3; note++)
                {
                    if (t >= startOffsets[note])
                    {
                        double dt = t - startOffsets[note];
                        double decay = Math.Exp(-4.0 * dt);
                        wave += Math.Sin(2 * Math.PI * noteFreqs[note] * dt) * decay * 0.33;
                    }
                }

                samples[i] = (short)(Math.Tanh(wave * 0.8) * short.MaxValue);
            }

            ApplyAlarmEnvelopes(samples, durationSeconds);
            SaveWavFile(filePath, samples, 1);
        }

        #endregion

        #region Focus Ambient Sound Generators (30-second loop WAVs, Mono/Stereo)

        private static void GenerateWhiteNoise(string filePath)
        {
            var rand = new Random();
            short[] samples = CreateSeamlessBuffer(30, 2.0, 1, (i, t, ch) =>
            {
                return (rand.NextDouble() * 2.0 - 1.0) * 0.15;
            });
            SaveWavFile(filePath, samples, 1);
        }

        private static void GeneratePinkNoise(string filePath)
        {
            var rand = new Random();
            double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
            short[] samples = CreateSeamlessBuffer(30, 2.0, 1, (i, t, ch) =>
            {
                double white = rand.NextDouble() * 2.0 - 1.0;
                b0 = 0.99886 * b0 + white * 0.0555179;
                b1 = 0.99332 * b1 + white * 0.0750759;
                b2 = 0.96900 * b2 + white * 0.1538520;
                b3 = 0.86650 * b3 + white * 0.3104856;
                b4 = 0.55000 * b4 + white * 0.5329522;
                b5 = -0.7616 * b5 - white * 0.0168980;
                double pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362;
                b6 = white * 0.115926;
                return pink * 0.035;
            });
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateBrownNoise(string filePath)
        {
            var rand = new Random();
            double accumulator = 0;
            short[] samples = CreateSeamlessBuffer(30, 2.0, 1, (i, t, ch) =>
            {
                double white = rand.NextDouble() * 2.0 - 1.0;
                accumulator = 0.98 * accumulator + 0.02 * white;
                return accumulator * 0.85;
            });
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateTicking(string filePath, double intervalSeconds)
        {
            short[] samples = CreateSeamlessBuffer(30, 2.0, 1, (i, t, ch) =>
            {
                double cycle = t % intervalSeconds;
                if (cycle < 0.015)
                {
                    double decay = Math.Exp(-250.0 * cycle);
                    double wave = Math.Sin(2 * Math.PI * 950.0 * cycle);
                    return wave * decay * 0.45;
                }
                return 0;
            });
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateRain(string filePath)
        {
            var rand = new Random();
            double pinkB0 = 0, pinkB1 = 0, pinkB2 = 0, pinkB3 = 0, pinkB4 = 0, pinkB5 = 0, pinkB6 = 0;
            var filter = new BiquadFilter();
            filter.SetBandPass(3500.0, 0.8); // Focus rain drop frequency band

            short[] samples = CreateSeamlessBuffer(30, 2.0, 1, (i, t, ch) =>
            {
                double white = rand.NextDouble() * 2.0 - 1.0;
                
                // Background Pink noise rumble
                pinkB0 = 0.99886 * pinkB0 + white * 0.0555179;
                pinkB1 = 0.99332 * pinkB1 + white * 0.0750759;
                pinkB2 = 0.96900 * pinkB2 + white * 0.1538520;
                pinkB3 = 0.86650 * pinkB3 + white * 0.3104856;
                pinkB4 = 0.55000 * pinkB4 + white * 0.5329522;
                pinkB5 = -0.7616 * pinkB5 - white * 0.0168980;
                double pink = pinkB0 + pinkB1 + pinkB2 + pinkB3 + pinkB4 + pinkB5 + pinkB6 + white * 0.5362;
                pinkB6 = white * 0.115926;

                // Poisson-like scattered rain drops
                double dropImpulse = 0;
                if (rand.NextDouble() < 0.0006)
                {
                    dropImpulse = (rand.NextDouble() * 2.0 - 1.0) * 0.7;
                }

                double dropFiltered = filter.Process(dropImpulse);
                return pink * 0.012 + dropFiltered * 0.4;
            });
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateOcean(string filePath)
        {
            var rand = new Random();
            double accumulator = 0;
            var filter = new BiquadFilter();

            short[] samples = CreateSeamlessBuffer(30, 2.0, 1, (i, t, ch) =>
            {
                double white = rand.NextDouble() * 2.0 - 1.0;
                accumulator = 0.98 * accumulator + 0.02 * white;

                // Modulation with two phase-shifted LFOs
                double lfo1 = 0.5 + 0.5 * Math.Sin(2 * Math.PI * t / 6.0); // 0.16Hz
                double lfo2 = 0.5 + 0.5 * Math.Cos(2 * Math.PI * t / 9.0); // 0.11Hz
                double waveAmp = (lfo1 * 0.6 + lfo2 * 0.4);

                // Dynamic Cutoff Sweep (Low cutoff for retraction, High for splash)
                double cutoff = 150.0 + 750.0 * waveAmp;
                filter.SetLowPass(cutoff, 0.7);

                double filtered = filter.Process(accumulator * 0.75);
                return filtered * waveAmp;
            });
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateCozyFireplace(string filePath)
        {
            var rand = new Random();
            double acc = 0;
            var lowFilter = new BiquadFilter();
            lowFilter.SetLowPass(150.0); // Filter for crackle bass thud
            
            var highFilter = new BiquadFilter();
            highFilter.SetHighPass(4000.0); // Crackle high frequency snap

            double thudSignal = 0;
            int thudDelayCount = 0;

            short[] samples = CreateSeamlessBuffer(30, 2.0, 1, (i, t, ch) =>
            {
                double white = rand.NextDouble() * 2.0 - 1.0;
                acc = 0.994 * acc + 0.006 * white; // Soft low rumble

                double snap = 0;
                // High frequency crackle triggers
                if (rand.NextDouble() < 0.00045)
                {
                    snap = (rand.NextDouble() * 2.0 - 1.0) * 0.8;
                    // Trigger delayed low-frequency thud
                    thudSignal = (rand.NextDouble() * 0.5 + 0.5) * (rand.NextDouble() > 0.5 ? 1.0 : -1.0);
                    thudDelayCount = (int)(SampleRate * (0.005 + rand.NextDouble() * 0.01)); // 5-15ms delay
                }

                double snapFiltered = highFilter.Process(snap);
                
                // Process delayed thud
                double thud = 0;
                if (thudDelayCount > 0)
                {
                    thudDelayCount--;
                    if (thudDelayCount == 0)
                    {
                        thud = thudSignal * 0.9;
                    }
                }
                double thudFiltered = lowFilter.Process(thud);

                return acc * 0.35 + snapFiltered * 0.45 + thudFiltered * 0.5;
            });
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateCoffeeShop(string filePath)
        {
            var rand = new Random();
            double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
            var bgFilter = new BiquadFilter();
            bgFilter.SetBandPass(1100.0, 0.7); // Muffled ambient sound frequency

            var clinkFilter = new BiquadFilter();
            clinkFilter.SetBandPass(3200.0, 2.0); // High pitched cup clinking

            short[] samples = CreateSeamlessBuffer(30, 2.0, 1, (i, t, ch) =>
            {
                double white = rand.NextDouble() * 2.0 - 1.0;
                
                // Pink noise base
                b0 = 0.99886 * b0 + white * 0.0555179;
                b1 = 0.99332 * b1 + white * 0.0750759;
                b2 = 0.96900 * b2 + white * 0.1538520;
                b3 = 0.86650 * b3 + white * 0.3104856;
                b4 = 0.55000 * b4 + white * 0.5329522;
                b5 = -0.7616 * b5 - white * 0.0168980;
                double pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362;
                b6 = white * 0.115926;

                double filteredBg = bgFilter.Process(pink * 0.075);

                // Random cup clinks (clacks)
                double clinkImpulse = 0;
                if (rand.NextDouble() < 0.00008)
                {
                    clinkImpulse = (rand.NextDouble() * 2.0 - 1.0) * 0.85;
                }
                
                double clinkFiltered = clinkFilter.Process(clinkImpulse);
                return filteredBg * 0.95 + clinkFiltered * 0.15;
            });
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateBinauralAlpha(string filePath)
        {
            // Stereo output required (2 channels)
            short[] samples = CreateSeamlessBuffer(30, 2.0, 2, (i, t, ch) =>
            {
                // Left channel: 200Hz, Right channel: 210Hz
                double freq = (ch == 0) ? 200.0 : 210.0;
                return Math.Sin(2 * Math.PI * freq * t) * 0.35;
            });
            SaveWavFile(filePath, samples, 2);
        }

        private static void GenerateSoftWind(string filePath)
        {
            var rand = new Random();
            double acc = 0;
            var filter = new BiquadFilter();

            short[] samples = CreateSeamlessBuffer(30, 2.0, 1, (i, t, ch) =>
            {
                double white = rand.NextDouble() * 2.0 - 1.0;
                acc = 0.985 * acc + 0.015 * white; // Brown noise base

                // LFO sweeps cutoff from 200Hz to 800Hz
                double sweep = 0.5 + 0.5 * Math.Sin(2 * Math.PI * 0.08 * t);
                double cutoff = 200.0 + 600.0 * sweep;
                
                filter.SetLowPass(cutoff, 0.9);
                return filter.Process(acc * 0.65);
            });
            SaveWavFile(filePath, samples, 1);
        }

        private static void GenerateNightCricket(string filePath)
        {
            var rand = new Random();
            double acc = 0;

            short[] samples = CreateSeamlessBuffer(30, 2.0, 1, (i, t, ch) =>
            {
                double white = rand.NextDouble() * 2.0 - 1.0;
                acc = 0.995 * acc + 0.005 * white; // Faint night wind base

                // Chirp sweep
                double rate = 5.0; // 5Hz chirp rate
                double cycle = t % (1.0 / rate);
                double activeTime = 0.08; // chirp active for 80ms

                double cricketSignal = 0;
                if (cycle < activeTime)
                {
                    // FM modulated cricket sound (4500Hz modulated by 150Hz)
                    double modulator = Math.Sin(2 * Math.PI * 150 * cycle);
                    double carrier = Math.Sin(2 * Math.PI * 4500 * cycle + 8.0 * modulator);
                    
                    // Amplitude envelope
                    double env = Math.Sin(Math.PI * (cycle / activeTime));
                    cricketSignal = carrier * env * 0.12;
                }

                return acc * 0.04 + cricketSignal;
            });
            SaveWavFile(filePath, samples, 1);
        }

        #endregion
    }
}
