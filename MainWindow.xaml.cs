using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pomodoro.Models;
using Pomodoro.ViewModels;

namespace Pomodoro
{
    public partial class MainWindow : Window
    {
        private TodoTask? _hoveredTask;
        private SpotifyWindow? _spotifyWindow;
        private DispatcherTimer? _gifTimer;
        private List<BitmapSource> _gifFrames = new List<BitmapSource>();
        private int _currentGifFrameIndex = 0;
        private RenderTargetBitmap? _gifAccumulator;
        private int _gifWidth;
        private int _gifHeight;
        private StrictModeOverlayWindow? _strictModeOverlayWindow;

        private WindowState _savedPreStrictWindowState = WindowState.Normal;
        private double _savedLeft;
        private double _savedTop;
        private double _savedWidth;
        private double _savedHeight;
        private bool _isStrictFullscreenActive = false;
        private bool _isUserF11FullscreenActive = false;

        private bool _isSidebarHoverExpanded = false;
        private System.Windows.Threading.DispatcherTimer? _sidebarAnimTimer;
        private System.Windows.Threading.DispatcherTimer? _sidebarLeaveDebounceTimer;
        private double _animStartWidth;
        private double _animTargetWidth;
        private double _animStartSplitterWidth;
        private double _animTargetSplitterWidth;
        private DateTime _animStartTime;
        private const double ANIM_DURATION_MS = 180;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private void ApplyStrictFullscreen(bool enable)
        {
            var handle = new WindowInteropHelper(this).Handle;

            if (enable)
            {
                if (!_isStrictFullscreenActive)
                {
                    _savedPreStrictWindowState = this.WindowState;
                    _savedLeft = this.Left;
                    _savedTop = this.Top;
                    _savedWidth = this.Width;
                    _savedHeight = this.Height;
                    _isStrictFullscreenActive = true;
                }

                if (CustomTitleBar != null) CustomTitleBar.Visibility = Visibility.Collapsed;
                if (FocusHeaderTitle != null) FocusHeaderTitle.Visibility = Visibility.Collapsed;
                if (MinimizeBtn != null) MinimizeBtn.IsEnabled = false;
                if (MaximizeBtn != null) MaximizeBtn.IsEnabled = false;

                TaskbarManager.HideTaskbar();
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.WindowState = WindowState.Normal;

                IntPtr monitor = MonitorFromWindow(handle, 2); // MONITOR_DEFAULTTONEAREST
                if (monitor != IntPtr.Zero)
                {
                    MONITORINFO monitorInfo = new MONITORINFO();
                    monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                    GetMonitorInfo(monitor, ref monitorInfo);

                    RECT r = monitorInfo.rcMonitor;

                    this.Left = r.Left;
                    this.Top = r.Top;
                    this.Width = r.Right - r.Left;
                    this.Height = r.Bottom - r.Top;
                }
                this.Topmost = true;

                if (handle != IntPtr.Zero)
                {
                    SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
                }
            }
            else
            {
                if (_isStrictFullscreenActive)
                {
                    _isStrictFullscreenActive = false;
                    this.Topmost = false;

                    if (CustomTitleBar != null) CustomTitleBar.Visibility = Visibility.Visible;
                    if (FocusHeaderTitle != null) FocusHeaderTitle.Visibility = Visibility.Visible;
                    TaskbarManager.ShowTaskbar();
                    this.ResizeMode = ResizeMode.CanResize;

                    if (MinimizeBtn != null) MinimizeBtn.IsEnabled = true;
                    if (MaximizeBtn != null) MaximizeBtn.IsEnabled = true;

                    if (_savedPreStrictWindowState == WindowState.Maximized)
                    {
                        this.WindowState = WindowState.Normal;
                        this.WindowState = WindowState.Maximized;
                    }
                    else
                    {
                        this.WindowState = WindowState.Normal;
                        this.Left = _savedLeft;
                        this.Top = _savedTop;
                        this.Width = _savedWidth;
                        this.Height = _savedHeight;
                    }

                    if (handle != IntPtr.Zero)
                    {
                        SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
                    }
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainViewModel();
            DataContext = viewModel;

            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.F11)
                {
                    e.Handled = true;
                    _isUserF11FullscreenActive = !_isUserF11FullscreenActive;
                    bool shouldBeFullscreen = _isUserF11FullscreenActive ||
                        (viewModel.IsRunning && viewModel.TimerMode == "Focus" && viewModel.Settings.IsStrictModeEnabled);
                    ApplyStrictFullscreen(shouldBeFullscreen);
                }
                else if (e.Key == System.Windows.Input.Key.Escape && _isStrictFullscreenActive)
                {
                    e.Handled = true;
                    _isUserF11FullscreenActive = false;
                    bool shouldBeFullscreen = viewModel.IsRunning && viewModel.TimerMode == "Focus" && viewModel.Settings.IsStrictModeEnabled;
                    ApplyStrictFullscreen(shouldBeFullscreen);
                }
            };

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
            UpdateDrawerColumnWidth(viewModel.IsTodoDrawerCollapsed, settings.TodoColumnWidth);

            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsTodoDrawerCollapsed) ||
                    e.PropertyName == nameof(MainViewModel.IsSidebarAutoHideEnabled))
                {
                    UpdateDrawerColumnWidth(viewModel.IsTodoDrawerCollapsed, settings.TodoColumnWidth);
                }
            };

            RootWindowGrid.MouseMove += (s, e) =>
            {
                if (DataContext is not MainViewModel vm) return;
                if (vm.IsTodoDrawerCollapsed && vm.Settings.IsSidebarAutoHideEnabled)
                {
                    var pos = e.GetPosition(RootWindowGrid);
                    if (_isSidebarHoverExpanded && pos.X > 90)
                    {
                        HideSidebarHover();
                    }
                    else if (!_isSidebarHoverExpanded && pos.X <= 25)
                    {
                        ShowSidebarHover();
                    }
                }
            };

            // Save window size, position, and column layout on close
            this.Closing += (s, e) =>
            {
                TaskbarManager.ShowTaskbar();

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
                
                // Save custom Todo column width ratio in proportional Star units (out of 10.0 total stars) if drawer is expanded
                if (!viewModel.IsTodoDrawerCollapsed)
                {
                    double totalAct = TodoColumn.ActualWidth + PomoColumn.ActualWidth;
                    if (totalAct > 0)
                    {
                        settings.TodoColumnWidth = (TodoColumn.ActualWidth / totalAct) * 10.0;
                    }
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

                viewModel.SaveSettingsOnClose();
            };

            this.StateChanged += (s, e) =>
            {
                if (this.WindowState == WindowState.Minimized)
                {
                    if (BackgroundVideoPlayer != null && BackgroundVideoPlayer.Visibility == Visibility.Visible)
                    {
                        BackgroundVideoPlayer.Pause();
                    }
                }
                else
                {
                    if (BackgroundVideoPlayer != null && BackgroundVideoPlayer.Visibility == Visibility.Visible)
                    {
                        BackgroundVideoPlayer.Play();
                    }
                    RootWindowGrid.Margin = new Thickness(0);
                    this.UpdateLayout();
                }
            };
            this.Activated += (s, e) => 
            {
                viewModel.OnAppActivated();
                if (_strictModeOverlayWindow != null)
                {
                    _strictModeOverlayWindow.Hide();
                }
            };
            this.Deactivated += (s, e) =>
            {
                viewModel.OnAppDeactivated();
                if (viewModel.IsRunning && viewModel.TimerMode == "Focus" && viewModel.Settings.IsStrictModeEnabled)
                {
                    if (_strictModeOverlayWindow == null)
                    {
                        _strictModeOverlayWindow = new StrictModeOverlayWindow(this);
                    }
                    _strictModeOverlayWindow.Show();
                    _strictModeOverlayWindow.Activate();
                }
            };

            // Subscribe to PropertyChanged to update background media and strict mode fullscreen/overlay
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.SelectedThemeName) ||
                    e.PropertyName == nameof(MainViewModel.Settings))
                {
                    UpdateBackgroundMedia();
                }
                else if (e.PropertyName == nameof(MainViewModel.IsStrictModeWarningVisible))
                {
                    if (!viewModel.IsStrictModeWarningVisible && _strictModeOverlayWindow != null && _strictModeOverlayWindow.IsVisible)
                    {
                        _strictModeOverlayWindow.Hide();
                    }
                }
                else if (e.PropertyName == nameof(MainViewModel.IsRunning) || e.PropertyName == nameof(MainViewModel.TimerMode))
                {
                    bool shouldBeFullscreen = _isUserF11FullscreenActive ||
                        (viewModel.IsRunning && viewModel.TimerMode == "Focus" && viewModel.Settings.IsStrictModeEnabled);

                    ApplyStrictFullscreen(shouldBeFullscreen);

                    if (!shouldBeFullscreen && _strictModeOverlayWindow != null && _strictModeOverlayWindow.IsVisible)
                    {
                        _strictModeOverlayWindow.Hide();
                    }
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

        private void ShowSidebarHover()
        {
            if (DataContext is not MainViewModel vm) return;
            if (!vm.IsTodoDrawerCollapsed || !vm.Settings.IsSidebarAutoHideEnabled) return;

            _sidebarLeaveDebounceTimer?.Stop();
            if (!_isSidebarHoverExpanded)
            {
                _isSidebarHoverExpanded = true;
                if (LeftEdgeHoverTrigger != null) LeftEdgeHoverTrigger.IsHitTestVisible = false;
                UpdateDrawerColumnWidth(true, vm.Settings.TodoColumnWidth, animate: true);
            }
        }

        private void HideSidebarHover()
        {
            if (DataContext is not MainViewModel vm) return;
            if (!vm.IsTodoDrawerCollapsed || !vm.Settings.IsSidebarAutoHideEnabled) return;

            if (_sidebarLeaveDebounceTimer == null)
            {
                _sidebarLeaveDebounceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
                _sidebarLeaveDebounceTimer.Tick += (s, e) =>
                {
                    _sidebarLeaveDebounceTimer.Stop();
                    var pos = System.Windows.Input.Mouse.GetPosition(RootWindowGrid);
                    if (pos.X > 80 || pos.Y < 0 || pos.Y > RootWindowGrid.ActualHeight)
                    {
                        _isSidebarHoverExpanded = false;
                        if (LeftEdgeHoverTrigger != null) LeftEdgeHoverTrigger.IsHitTestVisible = true;
                        UpdateDrawerColumnWidth(true, vm.Settings.TodoColumnWidth, animate: true);
                    }
                };
            }
            _sidebarLeaveDebounceTimer.Stop();
            _sidebarLeaveDebounceTimer.Start();
        }

        private void LeftEdgeHoverTrigger_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => ShowSidebarHover();
        private void LeftEdgeHoverTrigger_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) => ShowSidebarHover();
        private void TodoDrawerContainer_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => ShowSidebarHover();
        private void TodoDrawerContainer_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => HideSidebarHover();

        private void AnimateColumnWidths(double targetTodoWidth, double targetSplitterWidth)
        {
            if (_sidebarAnimTimer == null)
            {
                _sidebarAnimTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render);
                _sidebarAnimTimer.Interval = TimeSpan.FromMilliseconds(16);
                _sidebarAnimTimer.Tick += SidebarAnimTimer_Tick;
            }

            _animStartWidth = TodoColumn.Width.Value;
            _animTargetWidth = targetTodoWidth;

            _animStartSplitterWidth = SplitterColumn != null ? SplitterColumn.Width.Value : 0;
            _animTargetSplitterWidth = targetSplitterWidth;

            _animStartTime = DateTime.Now;
            _sidebarAnimTimer.Start();
        }

        private void SidebarAnimTimer_Tick(object? sender, EventArgs e)
        {
            double elapsed = (DateTime.Now - _animStartTime).TotalMilliseconds;
            double progress = Math.Min(1.0, elapsed / ANIM_DURATION_MS);
            double easeProgress = 1.0 - Math.Pow(1.0 - progress, 3);

            double currentTodoWidth = _animStartWidth + (_animTargetWidth - _animStartWidth) * easeProgress;
            double currentSplitterWidth = _animStartSplitterWidth + (_animTargetSplitterWidth - _animStartSplitterWidth) * easeProgress;

            TodoColumn.Width = new GridLength(Math.Max(0, currentTodoWidth));
            if (SplitterColumn != null)
            {
                SplitterColumn.Width = new GridLength(Math.Max(0, currentSplitterWidth));
            }

            if (progress >= 1.0)
            {
                _sidebarAnimTimer?.Stop();
                TodoColumn.Width = new GridLength(Math.Max(0, _animTargetWidth));
                if (SplitterColumn != null)
                {
                    SplitterColumn.Width = new GridLength(Math.Max(0, _animTargetSplitterWidth));
                }
            }
        }

        private void UpdateDrawerColumnWidth(bool isCollapsed, double savedStarWidth, bool animate = false)
        {
            if (DataContext is not MainViewModel vm) return;

            if (isCollapsed)
            {
                if (vm.Settings.IsSidebarAutoHideEnabled)
                {
                    double targetTodo = _isSidebarHoverExpanded ? 56 : 0;
                    double targetSplitter = _isSidebarHoverExpanded ? 24 : 0;
                    if (animate)
                    {
                        AnimateColumnWidths(targetTodo, targetSplitter);
                    }
                    else
                    {
                        _sidebarAnimTimer?.Stop();
                        TodoColumn.Width = new GridLength(targetTodo);
                        if (SplitterColumn != null) SplitterColumn.Width = new GridLength(targetSplitter);
                        PomoColumn.Width = new GridLength(1.0, GridUnitType.Star);
                    }
                }
                else
                {
                    _sidebarAnimTimer?.Stop();
                    TodoColumn.Width = new GridLength(56);
                    if (SplitterColumn != null) SplitterColumn.Width = new GridLength(32);
                    PomoColumn.Width = new GridLength(1.0, GridUnitType.Star);
                }
            }
            else
            {
                _sidebarAnimTimer?.Stop();
                double starVal = savedStarWidth != -1 ? Math.Max(1.0, Math.Min(9.0, savedStarWidth)) : 3.5;
                TodoColumn.Width = new GridLength(starVal, GridUnitType.Star);
                if (SplitterColumn != null) SplitterColumn.Width = new GridLength(32);
                PomoColumn.Width = new GridLength(10.0 - starVal, GridUnitType.Star);
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.IsRunning && vm.TimerMode == "Focus" && vm.Settings.IsStrictModeEnabled)
            {
                return;
            }
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.IsRunning && vm.TimerMode == "Focus" && vm.Settings.IsStrictModeEnabled)
            {
                return;
            }
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                this.Width = 1280;
                this.Height = 720;
                this.Left = (SystemParameters.WorkArea.Width - 1280) / 2 + SystemParameters.WorkArea.Left;
                this.Top = (SystemParameters.WorkArea.Height - 720) / 2 + SystemParameters.WorkArea.Top;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.IsRunning && vm.TimerMode == "Focus" && vm.Settings.IsStrictModeEnabled)
            {
                return;
            }
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    if (this.WindowState == WindowState.Maximized)
                    {
                        this.WindowState = WindowState.Normal;
                        this.Width = 1280;
                        this.Height = 720;
                        this.Left = (SystemParameters.WorkArea.Width - 1280) / 2 + SystemParameters.WorkArea.Left;
                        this.Top = (SystemParameters.WorkArea.Height - 720) / 2 + SystemParameters.WorkArea.Top;
                    }
                    else
                    {
                        this.WindowState = WindowState.Maximized;
                    }
                }
                else
                {
                    if (this.WindowState == WindowState.Maximized)
                    {
                        var point = PointToScreen(e.GetPosition(this));
                        this.WindowState = WindowState.Normal;
                        this.Width = 1280;
                        this.Height = 720;
                        this.Left = point.X - 1280 / 2;
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

                bool isStrictFocus = false;
                if (DataContext is MainViewModel vm)
                {
                    isStrictFocus = vm.IsRunning && vm.TimerMode == "Focus" && vm.Settings.IsStrictModeEnabled;
                }

                if (isStrictFocus || _isStrictFullscreenActive)
                {
                    // True Fullscreen covering entire monitor screen including taskbar
                    mmi.ptMaxPosition.X = 0;
                    mmi.ptMaxPosition.Y = 0;
                    mmi.ptMaxSize.X = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
                    mmi.ptMaxSize.Y = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;
                }
                else
                {
                    // Standard Maximized fitting within work area (taskbar visible)
                    mmi.ptMaxPosition.X = monitorInfo.rcWork.Left - monitorInfo.rcMonitor.Left;
                    mmi.ptMaxPosition.Y = monitorInfo.rcWork.Top - monitorInfo.rcMonitor.Top;
                    mmi.ptMaxSize.X = monitorInfo.rcWork.Right - monitorInfo.rcWork.Left;
                    mmi.ptMaxSize.Y = monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top;
                }

                // Enforce minimum window width (960) and height (540) in 16:9 ratio
                mmi.ptMinTrackSize.X = 960;
                mmi.ptMinTrackSize.Y = 540;
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

                    // Detect background color asynchronously on background thread to prevent UI freezing
                    Task.Run(() => GetBackgroundAverageColor(mediaPath)).ContinueWith(task =>
                    {
                        if (task.Status == TaskStatus.RanToCompletion && DataContext is MainViewModel currentVm)
                        {
                            Dispatcher.Invoke(() => UpdateThemeColorsForBackground(currentVm, task.Result));
                        }
                    });

                    // Set solid dark background on the player grid to prevent transparent GIF pixels bleeding the desktop/IDE through
                    BackgroundPlayerGrid.Background = new SolidColorBrush(Color.FromRgb(18, 18, 22));
                    vm.MainWindowBackgroundBrush = vm.SelectedBackgroundBrush;

                    bool isVideo = ext == ".mp4" || ext == ".wmv" || ext == ".avi" || ext == ".mov" || ext == ".mkv";

                    if (isVideo)
                    {
                        StopGifAnimation();
                        BackgroundImagePlayer.Visibility = Visibility.Collapsed;
                        BackgroundVideoPlayer.Visibility = Visibility.Visible;
                        
                        try
                        {
                            BackgroundVideoPlayer.Source = new Uri(mediaPath);
                            BackgroundVideoPlayer.Play();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to play background video: {ex.Message}");
                        }
                    }
                    else
                    {
                        BackgroundVideoPlayer.Stop();
                        BackgroundVideoPlayer.Visibility = Visibility.Collapsed;
                        BackgroundImagePlayer.Visibility = Visibility.Visible;

                        if (ext == ".gif")
                        {
                            PlayGifNative(mediaPath);
                        }
                        else
                        {
                            StopGifAnimation();
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
                    BackgroundPlayerGrid.Background = System.Windows.Media.Brushes.Transparent;
                    vm.MainWindowBackgroundBrush = vm.SelectedBackgroundBrush;
                    BackgroundVideoPlayer.Stop();
                    StopGifAnimation();
                    BackgroundVideoPlayer.Visibility = Visibility.Collapsed;
                    BackgroundImagePlayer.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void PlayGifNative(string gifPath)
        {
            try
            {
                StopGifAnimation();

                var uri = new Uri(gifPath);
                var decoder = new GifBitmapDecoder(uri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                
                _gifFrames.Clear();
                foreach (var frame in decoder.Frames)
                {
                    _gifFrames.Add(frame);
                }

                if (_gifFrames.Count == 0) return;

                var firstFrame = _gifFrames[0];
                _gifWidth = firstFrame.PixelWidth;
                _gifHeight = firstFrame.PixelHeight;
                
                // Create drawing canvas accumulator
                _gifAccumulator = new RenderTargetBitmap(_gifWidth, _gifHeight, 96, 96, PixelFormats.Pbgra32);
                
                RenderGifFrame(0);
                _currentGifFrameIndex = 0;

                if (_gifFrames.Count > 1)
                {
                    int delayMs = GetGifFrameDelay(decoder.Frames[0]);
                    _gifTimer = new DispatcherTimer(DispatcherPriority.Render);
                    _gifTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
                    _gifTimer.Tick += (s, e) =>
                    {
                        if (_gifFrames.Count == 0 || _gifAccumulator == null) return;
                        _currentGifFrameIndex = (_currentGifFrameIndex + 1) % _gifFrames.Count;
                        
                        RenderGifFrame(_currentGifFrameIndex);
                        
                        // Dynamically adjust interval for variable framerate GIFs
                        int nextDelay = GetGifFrameDelay(decoder.Frames[_currentGifFrameIndex]);
                        if (_gifTimer.Interval.TotalMilliseconds != nextDelay)
                        {
                            _gifTimer.Interval = TimeSpan.FromMilliseconds(nextDelay);
                        }
                    };
                    _gifTimer.Start();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load animated GIF natively: {ex.Message}");
            }
        }

        private void RenderGifFrame(int index)
        {
            if (_gifAccumulator == null || _gifFrames.Count <= index) return;
            
            var frame = _gifFrames[index];
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                if (index > 0)
                {
                    // Draw accumulated previous frames (to respect GIF transparency/combine disposal)
                    dc.DrawImage(_gifAccumulator, new Rect(0, 0, _gifWidth, _gifHeight));
                }
                else
                {
                    // Clean canvas for first frame
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(18, 18, 22)), null, new Rect(0, 0, _gifWidth, _gifHeight));
                }
                
                // Draw current frame on top
                dc.DrawImage(frame, new Rect(0, 0, _gifWidth, _gifHeight));
            }
            _gifAccumulator.Render(drawingVisual);
            BackgroundImagePlayer.Source = _gifAccumulator;
        }

        private void StopGifAnimation()
        {
            if (_gifTimer != null)
            {
                _gifTimer.Stop();
                _gifTimer = null;
            }
            _gifFrames.Clear();
            _gifAccumulator = null;
            _currentGifFrameIndex = 0;
        }

        private int GetGifFrameDelay(BitmapFrame frame)
        {
            try
            {
                var metadata = frame.Metadata as BitmapMetadata;
                if (metadata != null && metadata.ContainsQuery("/grctle/Delay"))
                {
                    object delay = metadata.GetQuery("/grctle/Delay");
                    if (delay != null)
                    {
                        int delayInt = Convert.ToInt32(delay); // in centiseconds (10ms)
                        if (delayInt > 0)
                        {
                            return delayInt * 10; // convert to milliseconds
                        }
                    }
                }
            }
            catch { }
            return 100; // default 100ms
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
                // For videos, default to dark theme for maximum readability
                return Color.FromRgb(15, 15, 20);
            }

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.DecodePixelWidth = 100; // Downscale to 100px for lightning-fast sampling without UI freeze
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // Make cross-thread safe

                // FormatConvert to 32-bit RGB
                var fcBitmap = new System.Windows.Media.Imaging.FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                fcBitmap.Freeze();

                int gridWidth = 10;
                int gridHeight = 10;
                int stepX = Math.Max(1, fcBitmap.PixelWidth / gridWidth);
                int stepY = Math.Max(1, fcBitmap.PixelHeight / gridHeight);

                Color bestColor = Color.FromRgb(244, 63, 94); // default rose
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
                return Color.FromRgb(244, 63, 94); // default rose
            }
        }

        private void UpdateThemeColorsForBackground(MainViewModel vm, Color avgColor)
        {
            // Calculate relative luminance: Y = 0.2126 * R + 0.7152 * G + 0.0722 * B
            double relativeLuminance = (0.2126 * avgColor.R + 0.7152 * avgColor.G + 0.0722 * avgColor.B) / 255.0;
            
            // Convert to HSL to fine-tune accent color
            ColorToHsl(avgColor, out double h, out double s, out double l);
            
            bool isDarkBackground = relativeLuminance < 0.45;
            Color accentColor;

            if (isDarkBackground)
            {
                // For dark backgrounds, we want a vibrant, highly-saturated neon accent
                accentColor = ColorFromHsl(h, 0.85, 0.65);
                
                // Blend avgColor (25%) with base dark grey (75%) for an integrated ultra-glass tint
                byte cardR = (byte)(0.25 * avgColor.R + 0.75 * 18);
                byte cardG = (byte)(0.25 * avgColor.G + 0.75 * 18);
                byte cardB = (byte)(0.25 * avgColor.B + 0.75 * 22);
                
                // Opacity set to ~18% (45/255) for maximum wallpaper transparency
                vm.CardBackgroundBrush = new SolidColorBrush(Color.FromArgb(45, cardR, cardG, cardB));
                vm.CardBorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)); // 15% white border
                vm.TextForegroundBrush = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                vm.SubTextForegroundBrush = new SolidColorBrush(Color.FromRgb(180, 180, 190));
                vm.TextBoxBackgroundBrush = new SolidColorBrush(Color.FromArgb(80, cardR, cardG, cardB));
            }
            else
            {
                // For light/neutral backgrounds, use soft white glass tint (rgba(255, 255, 255, 0.18))
                accentColor = ColorFromHsl(h, 0.85, 0.30);
                
                // Blend avgColor (20%) with pure white (80%) for soft white glass
                byte cardR = (byte)(0.20 * avgColor.R + 0.80 * 255);
                byte cardG = (byte)(0.20 * avgColor.G + 0.80 * 255);
                byte cardB = (byte)(0.20 * avgColor.B + 0.80 * 255);
                
                // Opacity set to ~18% (45/255) for ultra transparent light glass
                vm.CardBackgroundBrush = new SolidColorBrush(Color.FromArgb(45, cardR, cardG, cardB));
                vm.CardBorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)); // 15% white border
                vm.TextForegroundBrush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                vm.SubTextForegroundBrush = new SolidColorBrush(Color.FromRgb(90, 90, 100));
                vm.TextBoxBackgroundBrush = new SolidColorBrush(Color.FromArgb(90, cardR, cardG, cardB));
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

        private void Drawer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }

    public static class TaskbarManager
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        public static void HideTaskbar()
        {
            IntPtr hwnd = FindWindow("Shell_TrayWnd", string.Empty);
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_HIDE);
            }
        }

        public static void ShowTaskbar()
        {
            IntPtr hwnd = FindWindow("Shell_TrayWnd", string.Empty);
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_SHOW);
            }
        }
    }
}