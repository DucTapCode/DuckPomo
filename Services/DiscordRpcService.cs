using System;
using DiscordRPC;

namespace Pomodoro.Services
{
    public class DiscordRpcService
    {
        private DiscordRpcClient? _client;
        private const string ClientId = "1528781432271409287";

        public void Initialize()
        {
            try
            {
                _client = new DiscordRpcClient(ClientId);
                _client.Initialize();
            }
            catch (Exception)
            {
                // Silence initialization errors if Discord is not running or fails
                _client = null;
            }
        }

        public void UpdatePresence(string mode, string? taskTitle, bool isRunning, int timeLeftSeconds, int totalDurationSeconds)
        {
            if (_client == null || _client.IsDisposed) return;

            try
            {
                var presence = new RichPresence();

                presence.Assets = new Assets()
                {
                    LargeImageKey = "icon",
                    LargeImageText = "Pomodoro"
                };

                if (mode == "Focus")
                {
                    presence.Details = !string.IsNullOrEmpty(taskTitle) ? $"Focusing on: {taskTitle}" : "Focusing";
                    presence.State = isRunning ? "Working" : $"Paused ({timeLeftSeconds / 60:D2}:{timeLeftSeconds % 60:D2} left)";
                }
                else
                {
                    presence.Details = mode; // "Short Break" or "Long Break"
                    presence.State = isRunning ? "Resting" : $"Paused ({timeLeftSeconds / 60:D2}:{timeLeftSeconds % 60:D2} left)";
                }

                if (isRunning)
                {
                    presence.Timestamps = new Timestamps()
                    {
                        End = DateTime.UtcNow.AddSeconds(timeLeftSeconds)
                    };
                }
                else
                {
                    presence.Timestamps = null;
                }

                _client.SetPresence(presence);
            }
            catch (Exception)
            {
                // Silence updates if Discord loses connection
            }
        }

        public void Deinitialize()
        {
            try
            {
                if (_client != null)
                {
                    _client.ClearPresence();
                    _client.Dispose();
                    _client = null;
                }
            }
            catch (Exception)
            {
                // Silence disposal errors
            }
        }
    }
}
