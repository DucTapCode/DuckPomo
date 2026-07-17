using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Pomodoro.Models;

namespace Pomodoro.Services
{
    public class DataService : IDataService
    {
        private readonly string _filePath;
        private AppDataContainer _data;

        public DataService()
        {
            string appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PomodoroApp");
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            _filePath = Path.Combine(appFolder, "app_data.json");
            _data = LoadData();
        }

        private AppDataContainer LoadData()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var container = JsonSerializer.Deserialize<AppDataContainer>(json);
                    if (container != null)
                    {
                        container.Tasks ??= new List<TodoTask>();
                        container.History ??= new List<PomodoroSession>();
                        container.Settings ??= new AppSettings();
                        return container;
                    }
                }
            }
            catch (Exception)
            {
                // If read fails, fallback to clean slate
            }

            return new AppDataContainer
            {
                Tasks = new List<TodoTask>(),
                History = new List<PomodoroSession>(),
                Settings = new AppSettings()
            };
        }

        private void SaveData()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_data, options);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save data: {ex.Message}");
            }
        }

        public AppSettings GetSettings() => _data.Settings;

        public void SaveSettings(AppSettings settings)
        {
            _data.Settings = settings;
            SaveData();
        }

        public List<TodoTask> GetTasks() => _data.Tasks;

        public void SaveTasks(List<TodoTask> tasks)
        {
            _data.Tasks = tasks;
            SaveData();
        }

        public List<PomodoroSession> GetHistory() => _data.History;

        public void SaveHistory(List<PomodoroSession> history)
        {
            _data.History = history;
            SaveData();
        }

        public void SaveAll(List<TodoTask> tasks, List<PomodoroSession> history, AppSettings settings)
        {
            _data.Tasks = tasks;
            _data.History = history;
            _data.Settings = settings;
            SaveData();
        }

        private class AppDataContainer
        {
            public List<TodoTask> Tasks { get; set; } = new();
            public List<PomodoroSession> History { get; set; } = new();
            public AppSettings Settings { get; set; } = new();
        }
    }
}
