using System;
using System.Collections.Generic;

namespace Pomodoro.Models
{
    public class TodoTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium"; // "High", "Medium", "Low"
        public int EstimatedPomodoros { get; set; } = 1;
        public int CompletedPomodoros { get; set; } = 0;
        public string Tag { get; set; } = "Work"; // "Work", "Study", "Personal", etc.
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        // Advanced Trello-like features
        public bool IsWatched { get; set; }
        public List<string> Attachments { get; set; } = new();
    }
}
