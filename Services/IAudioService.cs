using System.Threading.Tasks;

namespace Pomodoro.Services
{
    public interface IAudioService
    {
        void PlayAlarm(string alarmType);
        Task StartAmbientAsync(string ambientType, double volume);
        void SetAmbientVolume(double volume);
        void StopAmbient();
    }
}
