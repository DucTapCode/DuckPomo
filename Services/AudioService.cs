using System;
using System.Windows.Media;

namespace Pomodoro.Services
{
    public class AudioService : IAudioService
    {
        private readonly MediaPlayer _ambientPlayer;
        private readonly MediaPlayer _alarmPlayer;
        private string _currentAmbientType = "None";

        public AudioService()
        {
            _ambientPlayer = new MediaPlayer();
            _ambientPlayer.MediaEnded += AmbientPlayer_MediaEnded;
            
            _alarmPlayer = new MediaPlayer();
        }

        private void AmbientPlayer_MediaEnded(object? sender, EventArgs e)
        {
            // Loop ambient sound
            _ambientPlayer.Position = TimeSpan.Zero;
            _ambientPlayer.Play();
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

        public void StartAmbient(string ambientType, double volume)
        {
            if (string.IsNullOrEmpty(ambientType) || ambientType == "None")
            {
                StopAmbient();
                return;
            }

            try
            {
                string path = AudioGenerator.GetSoundPath(ambientType);
                _currentAmbientType = ambientType;
                
                _ambientPlayer.Open(new Uri(path));
                _ambientPlayer.Volume = volume;
                _ambientPlayer.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play ambient: {ex.Message}");
            }
        }

        public void SetAmbientVolume(double volume)
        {
            _ambientPlayer.Volume = Math.Clamp(volume, 0.0, 1.0);
        }

        public void StopAmbient()
        {
            _ambientPlayer.Stop();
            _currentAmbientType = "None";
        }
    }
}
