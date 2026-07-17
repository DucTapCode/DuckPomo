namespace Pomodoro.Services
{
    public interface IAudioService
    {
        void PlayAlarm(string alarmType);
        void StartAmbient(string ambientType, double volume);
        void SetAmbientVolume(double volume);
        void StopAmbient();
    }
}
