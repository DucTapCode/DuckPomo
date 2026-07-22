using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Pomodoro.ViewModels;

namespace Pomodoro
{
    public partial class StrictModeOverlayWindow : Window
    {
        private readonly Window _mainWindow;

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public StrictModeOverlayWindow(Window mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            this.Owner = mainWindow;
            this.ShowInTaskbar = false;
            this.DataContext = mainWindow.DataContext;

            // Intercept Closing to hide instead of destroy
            this.Closing += (s, e) =>
            {
                e.Cancel = true;
                this.Hide();
            };

            this.MouseDown += (s, e) =>
            {
                this.Hide();
                if (_mainWindow.DataContext is MainViewModel vm)
                {
                    vm.OnAppActivated();
                }
                _mainWindow.Activate();
            };

            this.ReturnButton.Click += (s, e) =>
            {
                e.Handled = true;
                this.Hide();
                if (_mainWindow.DataContext is MainViewModel vm)
                {
                    vm.OnAppActivated();
                }
                _mainWindow.Activate();
            };

            this.GiveUpButton.Click += (s, e) =>
            {
                e.Handled = true;
                this.Hide();
                if (_mainWindow.DataContext is MainViewModel vm)
                {
                    vm.OnAppActivated();
                    vm.ResetTimerCommand.Execute(null);
                }
                _mainWindow.Activate();
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                int exStyle = GetWindowLong(handle, -20).ToInt32(); // GWL_EXSTYLE
                SetWindowLong(handle, -20, new IntPtr(exStyle | 0x00000080)); // WS_EX_TOOLWINDOW
            }
        }
    }
}
