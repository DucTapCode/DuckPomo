using System;
using System.Windows;

namespace Pomodoro
{
    public partial class WebWallpaperWindow : Window
    {
        private string _currentWebViewPath = string.Empty;
        private bool _isWebViewInitialized = false;

        public WebWallpaperWindow()
        {
            InitializeComponent();
        }

        public async void NavigateToHtml(string htmlPath)
        {
            if (string.IsNullOrEmpty(htmlPath))
            {
                this.Hide();
                return;
            }

            if (_currentWebViewPath == htmlPath)
            {
                this.Show();
                return;
            }

            _currentWebViewPath = htmlPath;

            try
            {
                this.Show();

                if (!_isWebViewInitialized)
                {
                    string userDataFolder = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "PomodoroApp", "WebView2UserData");
                    
                    var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    await WpeWebView.EnsureCoreWebView2Async(env);
                    _isWebViewInitialized = true;
                }

                string? folder = System.IO.Path.GetDirectoryName(htmlPath);
                if (string.IsNullOrEmpty(folder)) return;

                WpeWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "wpe.local",
                    folder,
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

                WpeWebView.Source = new Uri("http://wpe.local/" + System.IO.Path.GetFileName(htmlPath));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize WebWallpaper WebView2: {ex.Message}");
                this.Hide();
            }
        }

        public void ClearWallpaper()
        {
            _currentWebViewPath = string.Empty;
            if (_isWebViewInitialized)
            {
                try
                {
                    WpeWebView.Source = new Uri("about:blank");
                }
                catch { }
            }
            this.Hide();
        }
    }
}
