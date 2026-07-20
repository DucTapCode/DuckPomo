using System;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace Pomodoro.Services
{
    public class AudioService : IAudioService
    {
        private readonly MediaPlayer _alarmPlayer;
        private string _currentAmbientType = "None";

        // Win32 PlaySound P/Invoke
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool PlaySound(string? pszSound, IntPtr hmod, uint fdwSound);

        [DllImport("winmm.dll")]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        private const uint SND_ASYNC = 0x0001;
        private const uint SND_FILENAME = 0x00020000;
        private const uint SND_LOOP = 0x0008;
        private const uint SND_PURGE = 0x0040;
        private const uint SND_NODEFAULT = 0x0002;

        public AudioService()
        {
            _alarmPlayer = new MediaPlayer();
        }

        public void PlayAlarm(string alarmType)
        {
            if (string.IsNullOrEmpty(alarmType)) alarmType = "Kitchen";
            try
            {
                string path = AudioGenerator.GetSoundPath(alarmType);
                _alarmPlayer.Open(new Uri(path));
                _alarmPlayer.Volume = 1.0;
                _alarmPlayer.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play alarm: {ex.Message}");
            }
        }

        public async System.Threading.Tasks.Task StartAmbientAsync(string ambientType, double volume)
        {
            if (string.IsNullOrEmpty(ambientType) || ambientType == "None")
            {
                StopAmbient();
                return;
            }

            try
            {
                string path = await System.Threading.Tasks.Task.Run(() => AudioGenerator.GetSoundPath(ambientType));
                _currentAmbientType = ambientType;
                
                // Set volume first
                SetAmbientVolume(volume);

                // Play loopable sound using Win32 API (gapless)
                PlaySound(path, IntPtr.Zero, SND_ASYNC | SND_FILENAME | SND_LOOP | SND_NODEFAULT);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play ambient: {ex.Message}");
            }
        }

        public void SetAmbientVolume(double volume)
        {
            // Set process-level volume for waveOut devices (Win32 API)
            ushort volChan = (ushort)(Math.Clamp(volume, 0.0, 1.0) * 0xFFFF);
            uint dwVolume = ((uint)volChan << 16) | volChan;
            waveOutSetVolume(IntPtr.Zero, dwVolume);
        }

        public void StopAmbient()
        {
            PlaySound(null, IntPtr.Zero, SND_PURGE);
            _currentAmbientType = "None";
        }
    }
}
