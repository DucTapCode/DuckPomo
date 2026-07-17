using System;
using System.Windows;
using System.Windows.Input;

namespace Pomodoro
{
    public partial class SpotifyWindow : Window
    {
        public bool IsAppShuttingDown { get; set; } = false;

        public SpotifyWindow()
        {
            InitializeComponent();
            InitializeSpotifyWebView();
        }

        private async void InitializeSpotifyWebView()
        {
            try
            {
                string userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    "PomodoroApp", "SpotifyWebView2UserData");
                
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await SpotifyPlayerWebView.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize Spotify Window WebView2: {ex.Message}");
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!IsAppShuttingDown)
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }
    }
}
