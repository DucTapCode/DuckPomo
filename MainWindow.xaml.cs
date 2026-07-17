using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Pomodoro.Models;
using Pomodoro.ViewModels;

namespace Pomodoro
{
    public partial class MainWindow : Window
    {
        private TodoTask? _hoveredTask;
        private SpotifyWindow? _spotifyWindow;
        private WebWallpaperWindow? _webWallpaperWindow;

        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainViewModel();
            DataContext = viewModel;

            // Load saved window size and position
            var settings = viewModel.Settings;
            if (settings.WindowLeft != -1 && settings.WindowTop != -1)
            {
                // Safety check: ensure coordinates are within the virtual screen bounds to prevent spawning offscreen
                if (settings.WindowLeft + settings.WindowWidth > SystemParameters.VirtualScreenLeft &&
                    settings.WindowLeft < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
                    settings.WindowTop + settings.WindowHeight > SystemParameters.VirtualScreenTop &&
                    settings.WindowTop < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
                {
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                    this.Left = settings.WindowLeft;
                    this.Top = settings.WindowTop;
                    this.Width = settings.WindowWidth;
                    this.Height = settings.WindowHeight;
                }
                
                if (Enum.TryParse<WindowState>(settings.WindowState, out var state))
                {
                    this.WindowState = state;
                }
            }

            // Load saved Todo column layout using proportional star-based sizing (keeps dynamic stretching intact)
            if (settings.TodoColumnWidth != -1)
            {
                // Constrain settings.TodoColumnWidth to be within a reasonable star range (e.g. 1.0 to 9.0)
                double starVal = Math.Max(1.0, Math.Min(9.0, settings.TodoColumnWidth));
                TodoColumn.Width = new GridLength(starVal, GridUnitType.Star);
                PomoColumn.Width = new GridLength(10.0 - starVal, GridUnitType.Star);
            }

            // Save window size, position, and column layout on close
            this.Closing += (s, e) =>
            {
                if (this.WindowState == WindowState.Normal)
                {
                    settings.WindowLeft = this.Left;
                    settings.WindowTop = this.Top;
                    settings.WindowWidth = this.Width;
                    settings.WindowHeight = this.Height;
                }
                else
                {
                    var bounds = this.RestoreBounds;
                    settings.WindowLeft = bounds.Left;
                    settings.WindowTop = bounds.Top;
                    settings.WindowWidth = bounds.Width;
                    settings.WindowHeight = bounds.Height;
                }
                settings.WindowState = this.WindowState.ToString();
                
                // Save custom Todo column width ratio in proportional Star units (out of 10.0 total stars)
                double totalAct = TodoColumn.ActualWidth + PomoColumn.ActualWidth;
                if (totalAct > 0)
                {
                    settings.TodoColumnWidth = (TodoColumn.ActualWidth / totalAct) * 10.0;
                }
                
                // Clear temporary Wallpaper Engine extraction cache to prevent disk bloating
                try
                {
                    string wpeCachePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wpe_cache");
                    if (System.IO.Directory.Exists(wpeCachePath))
                    {
                        System.IO.Directory.Delete(wpeCachePath, true);
                    }
                }
                catch
                {
                }

                if (_spotifyWindow != null)
                {
                    _spotifyWindow.IsAppShuttingDown = true;
                    _spotifyWindow.Close();
                }

                if (_webWallpaperWindow != null)
                {
                    _webWallpaperWindow.Close();
                }

                viewModel.SaveSettingsOnClose();
            };

            // Initialize Web Wallpaper background window
            _webWallpaperWindow = new WebWallpaperWindow();
            
            this.LocationChanged += (s, e) => UpdateWebWallpaperPosition();
            this.SizeChanged += (s, e) => UpdateWebWallpaperPosition();
            this.StateChanged += (s, e) =>
            {
                if (_webWallpaperWindow != null)
                {
                    if (this.WindowState == WindowState.Minimized)
                    {
                        _webWallpaperWindow.WindowState = WindowState.Minimized;
                    }
                    else if (this.WindowState == WindowState.Maximized)
                    {
                        _webWallpaperWindow.WindowState = WindowState.Maximized;
                    }
                    else
                    {
                        _webWallpaperWindow.WindowState = WindowState.Normal;
                        UpdateWebWallpaperPosition();
                    }
                }
            };
            this.Activated += (s, e) => 
            {
                viewModel.OnAppActivated();
                KeepWallpaperBehind();
            };
            this.Deactivated += (s, e) => viewModel.OnAppDeactivated();

            // Subscribe to PropertyChanged to update background media
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.SelectedThemeName) ||
                    e.PropertyName == nameof(MainViewModel.Settings))
                {
                    UpdateBackgroundMedia();
                }
            };
            this.Loaded += (s, e) => UpdateBackgroundMedia();

            // Shortcut key: F to focus Search Box
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.F && !(Keyboard.FocusedElement is TextBox))
                {
                    this.SearchBox.Focus();
                    e.Handled = true;
                }
            };

            // Shortcut key: Space to select hovered task card
            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Space && !(Keyboard.FocusedElement is TextBox))
                {
                    if (_hoveredTask != null)
                    {
                        viewModel.SelectFocusTaskCommand.Execute(_hoveredTask);
                        e.Handled = true;
                    }
                }
            };

            try
            {
                string iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(iconPath));
                    this.Icon = bitmap;
                    this.TitleBarIcon.Source = bitmap;
                }
            }
            catch
            {
                // Silent fallback if icon is missing
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                }
                else
                {
                    if (this.WindowState == WindowState.Maximized)
                    {
                        var point = PointToScreen(e.GetPosition(this));
                        this.WindowState = WindowState.Normal;
                        this.Left = point.X - this.Width / 2;
                        this.Top = point.Y - 20;
                    }
                    this.DragMove();
                }
            }
        }

        #region Hover & Drag-Drop event handlers

        private void TaskCard_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TodoTask task)
            {
                _hoveredTask = task;
            }
        }

        private void TaskCard_MouseLeave(object sender, MouseEventArgs e)
        {
            _hoveredTask = null;
        }

        private void TaskCard_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && sender is FrameworkElement fe && fe.DataContext is TodoTask task)
                {
                    if (DataContext is MainViewModel vm)
                    {
                        foreach (var file in files)
                        {
                            vm.AddAttachmentCommand.Execute(new object[] { task, file });
                        }
                    }
                }
            }
        }

        private void RemoveAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is string filePath)
            {
                var parentBorder = FindAncestor<Border>(fe);
                if (parentBorder != null && parentBorder.DataContext is TodoTask task && DataContext is MainViewModel vm)
                {
                    vm.RemoveAttachmentCommand.Execute(new object[] { task, filePath });
                }
            }
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T ancestor) return ancestor;
                current = VisualTreeHelper.GetParent(current);
            } while (current != null);
            return null;
        }

        #endregion

        #region WM_GETMINMAXINFO Maximization Hook

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle).AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024) // WM_GETMINMAXINFO
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;

            IntPtr monitor = MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST

            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                GetMonitorInfo(monitor, ref monitorInfo);

                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;

                mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        #endregion

        private void BackgroundVideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Seek to a tiny offset (1ms) to force WPF MediaElement's native loop reset
            BackgroundVideoPlayer.Position = TimeSpan.FromMilliseconds(1);
            BackgroundVideoPlayer.Play();
        }

        private void UpdateBackgroundMedia()
        {
            if (DataContext is MainViewModel vm)
            {
                var theme = vm.SelectedThemeName;
                var mediaPath = vm.Settings.CustomBackgroundImagePath;

                // Stop any current animation on the image player
                BackgroundImagePlayer.BeginAnimation(Image.SourceProperty, null);

                if (theme == "Custom Image" && !string.IsNullOrEmpty(mediaPath) && System.IO.File.Exists(mediaPath))
                {
                    string ext = System.IO.Path.GetExtension(mediaPath).ToLower();

                    // Detect background color and synchronize theme accent/cards
                    Color avgColor = GetBackgroundAverageColor(mediaPath);
                    UpdateThemeColorsForBackground(vm, avgColor);

                    if (ext == ".html" || ext == ".htm")
                    {
                        BackgroundVideoPlayer.Stop();
                        BackgroundVideoPlayer.Visibility = Visibility.Collapsed;
                        BackgroundImagePlayer.Visibility = Visibility.Collapsed;

                        vm.MainWindowBackgroundBrush = System.Windows.Media.Brushes.Transparent;
                        if (_webWallpaperWindow != null)
                        {
                            _webWallpaperWindow.NavigateToHtml(mediaPath);
                            UpdateWebWallpaperPosition();
                        }
                    }
                    else
                    {
                        if (_webWallpaperWindow != null)
                        {
                            _webWallpaperWindow.ClearWallpaper();
                        }
                        vm.MainWindowBackgroundBrush = vm.SelectedBackgroundBrush;

                        bool isVideoOrGif = ext == ".mp4" || ext == ".wmv" || ext == ".avi" || ext == ".mov" || ext == ".mkv" || ext == ".gif";

                        if (isVideoOrGif)
                        {
                            BackgroundImagePlayer.Visibility = Visibility.Collapsed;
                            BackgroundVideoPlayer.Visibility = Visibility.Visible;
                            
                            try
                            {
                                BackgroundVideoPlayer.Source = new Uri(mediaPath);
                                BackgroundVideoPlayer.Play();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to play background video/GIF: {ex.Message}");
                            }
                        }
                        else
                        {
                            BackgroundVideoPlayer.Stop();
                            BackgroundVideoPlayer.Visibility = Visibility.Collapsed;
                            BackgroundImagePlayer.Visibility = Visibility.Visible;

                            try
                            {
                                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                                bitmap.BeginInit();
                                bitmap.UriSource = new Uri(mediaPath);
                                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                                bitmap.EndInit();
                                BackgroundImagePlayer.Source = bitmap;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to load background image: {ex.Message}");
                                BackgroundImagePlayer.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                }
                else
                {
                    if (_webWallpaperWindow != null)
                    {
                        _webWallpaperWindow.ClearWallpaper();
                    }
                    vm.MainWindowBackgroundBrush = vm.SelectedBackgroundBrush;
                    BackgroundVideoPlayer.Stop();
                    BackgroundVideoPlayer.Visibility = Visibility.Collapsed;
                    BackgroundImagePlayer.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UpdateWebWallpaperPosition()
        {
            if (_webWallpaperWindow != null)
            {
                _webWallpaperWindow.Left = this.Left;
                _webWallpaperWindow.Top = this.Top;
                _webWallpaperWindow.Width = this.Width;
                _webWallpaperWindow.Height = this.Height;
                KeepWallpaperBehind();
            }
        }

        private void KeepWallpaperBehind()
        {
            if (_webWallpaperWindow == null) return;
            try
            {
                var wwh = new WindowInteropHelper(_webWallpaperWindow).Handle;
                var mwh = new WindowInteropHelper(this).Handle;
                SetWindowPos(wwh, mwh, 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0010); // SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
            }
            catch { }
        }

        private Color GetAverageColorFromBitmap(BitmapSource bitmap)
        {
            try
            {
                var fcBitmap = new System.Windows.Media.Imaging.FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                int gridWidth = 8;
                int gridHeight = 8;
                int stepX = Math.Max(1, fcBitmap.PixelWidth / gridWidth);
                int stepY = Math.Max(1, fcBitmap.PixelHeight / gridHeight);

                Color bestColor = Color.FromRgb(244, 63, 94);
                double maxSaturation = -1;
                long sumR = 0, sumG = 0, sumB = 0, count = 0;

                for (int y = 0; y < fcBitmap.PixelHeight; y += stepY)
                {
                    for (int x = 0; x < fcBitmap.PixelWidth; x += stepX)
                    {
                        byte[] pixel = new byte[4];
                        var rect = new Int32Rect(x, y, 1, 1);
                        fcBitmap.CopyPixels(rect, pixel, 4, 0);

                        byte b = pixel[0];
                        byte g = pixel[1];
                        byte r = pixel[2];

                        sumR += r;
                        sumG += g;
                        sumB += b;
                        count++;

                        Color c = Color.FromRgb(r, g, b);
                        ColorToHsl(c, out double h, out double s, out double l);

                        if (s > maxSaturation && l > 0.2 && l < 0.8)
                        {
                            maxSaturation = s;
                            bestColor = c;
                        }
                    }
                }

                if (maxSaturation < 0.15 && count > 0)
                {
                    return Color.FromRgb((byte)(sumR / count), (byte)(sumG / count), (byte)(sumB / count));
                }

                return bestColor;
            }
            catch
            {
                return Color.FromRgb(244, 63, 94);
            }
        }

        private Color GetBackgroundAverageColor(string filePath)
        {
            string ext = System.IO.Path.GetExtension(filePath).ToLower();
            bool isImageOrGif = ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif";
            
            if (!isImageOrGif)
            {
                // For videos, use a modern high-quality cyber-accent color (glowing sky blue)
                return Color.FromRgb(56, 189, 248); // sky-300
            }

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                // FormatConvert to 32-bit RGB
                var fcBitmap = new System.Windows.Media.Imaging.FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);

                // Sample a 8x8 grid of pixels to find the most vibrant color
                int gridWidth = 8;
                int gridHeight = 8;
                int stepX = Math.Max(1, fcBitmap.PixelWidth / gridWidth);
                int stepY = Math.Max(1, fcBitmap.PixelHeight / gridHeight);

                Color bestColor = Color.FromRgb(244, 63, 94); // default rose
                double maxSaturation = -1;

                // Also calculate average color as fallback
                long sumR = 0, sumG = 0, sumB = 0, count = 0;

                for (int y = 0; y < fcBitmap.PixelHeight; y += stepY)
                {
                    for (int x = 0; x < fcBitmap.PixelWidth; x += stepX)
                    {
                        byte[] pixel = new byte[4];
                        var rect = new Int32Rect(x, y, 1, 1);
                        fcBitmap.CopyPixels(rect, pixel, 4, 0);

                        byte b = pixel[0];
                        byte g = pixel[1];
                        byte r = pixel[2];

                        sumR += r;
                        sumG += g;
                        sumB += b;
                        count++;

                        Color c = Color.FromRgb(r, g, b);
                        ColorToHsl(c, out double h, out double s, out double l);

                        // We prefer colors that are highly saturated and in a legible lightness range (0.2 to 0.8)
                        if (s > maxSaturation && l > 0.2 && l < 0.8)
                        {
                            maxSaturation = s;
                            bestColor = c;
                        }
                    }
                }

                // If the most saturated color is very dull (e.g. grayscale background), use average color
                if (maxSaturation < 0.15 && count > 0)
                {
                    return Color.FromRgb((byte)(sumR / count), (byte)(sumG / count), (byte)(sumB / count));
                }

                return bestColor;
            }
            catch
            {
                return Color.FromRgb(244, 63, 94); // default rose
            }
        }

        private void UpdateThemeColorsForBackground(MainViewModel vm, Color avgColor)
        {
            // Convert to HSL to fine-tune
            ColorToHsl(avgColor, out double h, out double s, out double l);

            // Safety threshold: Only classify as light theme if background is very light (L >= 0.75)
            // Purple, medium blue, or standard vibrant colors will correctly fall into Dark theme
            bool isDarkBackground = l < 0.75;
            Color accentColor;

            if (isDarkBackground)
            {
                // For dark backgrounds, we want a vibrant, highly-saturated neon accent
                accentColor = ColorFromHsl(h, 0.85, 0.65);
                
                // Adaptive dark theme brushes (glassmorphism style with ~15% opacity card backgrounds)
                vm.CardBackgroundBrush = new SolidColorBrush(Color.FromArgb(40, 15, 15, 20));
                vm.CardBorderBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                vm.TextForegroundBrush = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                vm.SubTextForegroundBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                vm.TextBoxBackgroundBrush = new SolidColorBrush(Color.FromArgb(60, 10, 10, 12));
            }
            else
            {
                // For light backgrounds, we want a dark, high-contrast readable accent
                accentColor = ColorFromHsl(h, 0.80, 0.35);
                
                // Adaptive light theme brushes (glassmorphism style with ~15% opacity white backgrounds)
                vm.CardBackgroundBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                vm.CardBorderBrush = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));
                vm.TextForegroundBrush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                vm.SubTextForegroundBrush = new SolidColorBrush(Color.FromRgb(113, 113, 122));
                vm.TextBoxBackgroundBrush = new SolidColorBrush(Color.FromArgb(60, 244, 244, 245));
            }

            vm.SelectedAccentBrush = new SolidColorBrush(accentColor);
        }

        private static void ColorToHsl(Color color, out double h, out double s, out double l)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));

            h = 0;
            s = 0;
            l = (max + min) / 2.0;

            if (max != min)
            {
                double d = max - min;
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

                if (max == r)
                {
                    h = (g - b) / d + (g < b ? 6 : 0);
                }
                else if (max == g)
                {
                    h = (b - r) / d + 2;
                }
                else if (max == b)
                {
                    h = (r - g) / d + 4;
                }

                h /= 6.0;
            }
        }

        private static Color ColorFromHsl(double h, double s, double l)
        {
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l; // achromatic
            }
            else
            {
                Func<double, double, double, double> hue2rgb = (p, q, t) =>
                {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
                    if (t < 1.0 / 2.0) return q;
                    if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
                    return p;
                };

                double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
                double p = 2.0 * l - q;

                r = hue2rgb(p, q, h + 1.0 / 3.0);
                g = hue2rgb(p, q, h);
                b = hue2rgb(p, q, h - 1.0 / 3.0);
            }

            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private void SpotifyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_spotifyWindow == null || !_spotifyWindow.IsLoaded)
            {
                _spotifyWindow = new SpotifyWindow();
                _spotifyWindow.Owner = this;
                _spotifyWindow.Show();
            }
            else
            {
                if (_spotifyWindow.Visibility == Visibility.Visible)
                {
                    _spotifyWindow.Hide();
                }
                else
                {
                    _spotifyWindow.Show();
                    _spotifyWindow.Focus();
                }
            }
        }
    }
}