using System.Collections.Generic;
using Pomodoro.Models;

namespace Pomodoro.Services
{
    public interface IDataService
    {
        AppSettings GetSettings();
        void SaveSettings(AppSettings settings);
        List<TodoTask> GetTasks();
        void SaveTasks(List<TodoTask> tasks);
        List<PomodoroSession> GetHistory();
        void SaveHistory(List<PomodoroSession> history);
        void SaveAll(List<TodoTask> tasks, List<PomodoroSession> history, AppSettings settings);
    }
}
