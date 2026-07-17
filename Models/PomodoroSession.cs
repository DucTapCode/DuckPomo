using System;

namespace Pomodoro.Models
{
    public class PomodoroSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public string Tag { get; set; } = "Work";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double DurationMinutes { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class TagStatItem
    {
        public string TagName { get; set; } = string.Empty;
        public double Minutes { get; set; }
        public string Color { get; set; } = "#FFFFFF";
    }
}
