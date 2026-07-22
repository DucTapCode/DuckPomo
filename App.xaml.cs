using System;
using System.IO;
using System.Windows;

namespace Pomodoro;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static System.Threading.Mutex? _appMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _appMutex = new System.Threading.Mutex(true, "DuckPomodoroAppMutex", out bool createdNew);
        base.OnStartup(e);
        CreateStartMenuShortcut();
    }

    private static void CreateStartMenuShortcut()
    {
        try
        {
            string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            string startMenuPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs\Duck Pomodoro.lnk"
            );

            string script = $"$WshShell = New-Object -ComObject WScript.Shell; " +
                           $"$Shortcut = $WshShell.CreateShortcut('{startMenuPath}'); " +
                           $"$Shortcut.TargetPath = '{exePath}'; " +
                           $"$Shortcut.WorkingDirectory = '{Path.GetDirectoryName(exePath)}'; " +
                           $"$Shortcut.Save()";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create shortcut: {ex.Message}");
        }
    }
}

