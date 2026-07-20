using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pomodoro.Models;
using Pomodoro.Services;

namespace Pomodoro.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly IAudioService _audioService;
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _strictModeTimer;
        private readonly DiscordRpcService _discordRpcService;

        // State variables
        private List<TodoTask> _allTasks = new();
        private ObservableCollection<TodoTask> _tasks = new();
        private TodoTask? _activeFocusTask;
        private AppSettings _settings;
        private int _timeLeftSeconds;
        private int _totalSessionDurationSeconds;
        private bool _isRunning;
        private string _timerMode = "Focus"; // "Focus", "Short Break", "Long Break"
        private double _progressValue;
        private string _timerText = "25:00";
        
        // Strict Mode state
        private bool _isStrictModeWarningVisible;
        private int _strictModeCountdown = 5;
        private bool _isSettingsVisible;
        private string _inAppNotificationMessage = string.Empty;
        private bool _isInAppNotificationVisible;

        // Advanced Trello-like state
        private string _searchQuery = string.Empty;
        private string _multiLinePasteText = string.Empty;
        private bool _isMultiLinePastePromptVisible;
        private string _selectedThemeName = "Deep OLED";
        
        // Themes color brushes (bound dynamically to UI elements)
        private Brush _selectedBackgroundBrush = Brushes.Black;
        private Brush _selectedAccentBrush = Brushes.Tomato;
        private Brush _cardBackgroundBrush = new SolidColorBrush(Color.FromRgb(18, 18, 20));
        private Brush _cardBorderBrush = new SolidColorBrush(Color.FromRgb(34, 34, 38));
        private Brush _textForegroundBrush = new SolidColorBrush(Color.FromRgb(244, 244, 245));
        private Brush _subTextForegroundBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
        private Brush _textBoxBackgroundBrush = new SolidColorBrush(Color.FromRgb(24, 24, 27));

        // Form Fields for New Task
        private string _newTaskTitle = string.Empty;
        private string _newTaskPriority = "Medium";
        private int _newTaskEstimate = 1;
        private string _newTaskTag = "Work";

        // Statistics properties
        private int _statsCompletedToday;
        private int _statsCompletedThisWeek;
        private double _statsTaskCompletionRate;
        private List<TagStatItem> _statsTagAllocation = new();

        public MainViewModel()
        {
            _dataService = new DataService();
            _audioService = new AudioService();
            _discordRpcService = new DiscordRpcService();
            _discordRpcService.Initialize();
            
            _settings = _dataService.GetSettings();
            if (_settings.BackgroundSaturation == 50 && !_settings.ShowColorCustom)
            {
                _settings.BackgroundSaturation = 100;
            }
            _allTasks = _dataService.GetTasks();

            // Restore theme
            _selectedThemeName = _settings.SelectedThemeName ?? "Deep OLED";
            UpdateBackgroundBrush();

            // Set initial timer values
            ResetTimerValues();

            // Setup Core Timer (runs every second)
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            // Setup Strict Mode checking Timer (runs every 1 second)
            _strictModeTimer = new DispatcherTimer();
            _strictModeTimer.Interval = TimeSpan.FromSeconds(1);
            _strictModeTimer.Tick += StrictModeTimer_Tick;

            // Wire up Commands
            AddTaskCommand = new RelayCommand(AddTask);
            DeleteTaskCommand = new RelayCommand<TodoTask>(DeleteTask);
            ToggleCompleteTaskCommand = new RelayCommand<TodoTask>(ToggleCompleteTask);
            SelectFocusTaskCommand = new RelayCommand<TodoTask>(SelectFocusTask);
            
            StartTimerCommand = new RelayCommand(StartTimer);
            PauseTimerCommand = new RelayCommand(PauseTimer);
            ResetTimerCommand = new RelayCommand(ResetTimer);
            
            SwitchModeCommand = new RelayCommand<string>(SwitchMode);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            ToggleSettingsCommand = new RelayCommand<object>(param =>
            {
                if (param?.ToString() != "SuppressClose")
                {
                    IsSettingsVisible = !IsSettingsVisible;
                }
            });
            CloseNotificationCommand = new RelayCommand(() => IsInAppNotificationVisible = false);
            ToggleSpotifyPlayerCommand = new RelayCommand(() => IsSpotifyPlayerVisible = !IsSpotifyPlayerVisible);
            NavigateToSpotifyLoginCommand = new RelayCommand(() => SpotifyCurrentUrl = "https://open.spotify.com/");
            NavigateToSpotifyPlayerCommand = new RelayCommand(() => SpotifyCurrentUrl = SpotifyPlaylistUrl);

            // Advanced Task Commands
            CloneTaskCommand = new RelayCommand<TodoTask>(CloneTask);
            ToggleWatchTaskCommand = new RelayCommand<TodoTask>(ToggleWatchTask);
            OpenAttachmentCommand = new RelayCommand<string>(OpenAttachment);
            AddAttachmentCommand = new RelayCommand<object>(AddAttachment);
            RemoveAttachmentCommand = new RelayCommand<object>(RemoveAttachment);

            SplitMultiLinePasteCommand = new RelayCommand(SplitMultiLinePaste);
            AddMultiLinePasteAsSingleCommand = new RelayCommand(AddMultiLinePasteAsSingle);
            SelectThemeCommand = new RelayCommand<string>(SelectTheme);
            ChooseCustomImageCommand = new RelayCommand(ChooseCustomImage);

            // Sync collection and run statistics
            ApplyFilters();
            UpdateStatistics();

            // Auto-select first task as active if any exists
            if (_allTasks.Any(t => !t.IsCompleted))
            {
                ActiveFocusTask = _allTasks.First(t => !t.IsCompleted);
            }
            UpdateDiscordPresence();
        }

        #region Properties

        public ObservableCollection<TodoTask> Tasks
        {
            get => _tasks;
            set => SetProperty(ref _tasks, value);
        }

        public TodoTask? ActiveFocusTask
        {
            get => _activeFocusTask;
            set
            {
                if (SetProperty(ref _activeFocusTask, value))
                {
                    UpdateDiscordPresence();
                }
            }
        }

        public AppSettings Settings
        {
            get => _settings;
            set
            {
                if (SetProperty(ref _settings, value))
                {
                    OnPropertyChanged(nameof(AmbientVolume));
                    OnPropertyChanged(nameof(SelectedAmbientSound));
                    OnPropertyChanged(nameof(SelectedAlarmSound));
                }
            }
        }

        public string TimerText
        {
            get => _timerText;
            set => SetProperty(ref _timerText, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public string TimerMode
        {
            get => _timerMode;
            set => SetProperty(ref _timerMode, value);
        }

        public bool IsSettingsVisible
        {
            get => _isSettingsVisible;
            set
            {
                if (SetProperty(ref _isSettingsVisible, value) && value)
                {
                    IsSpotifyPlayerVisible = false;
                }
            }
        }

        private bool _isSpotifyPlayerVisible;
        public bool IsSpotifyPlayerVisible
        {
            get => _isSpotifyPlayerVisible;
            set
            {
                if (SetProperty(ref _isSpotifyPlayerVisible, value) && value)
                {
                    IsSettingsVisible = false;
                }
            }
        }

        public string SpotifyPlaylistUrl
        {
            get => _settings.SpotifyPlaylistUrl;
            set
            {
                string formatted = FormatSpotifyUrl(value);
                if (_settings.SpotifyPlaylistUrl != formatted)
                {
                    _settings.SpotifyPlaylistUrl = formatted;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(SpotifyPlaylistUrl));
                    SpotifyCurrentUrl = formatted;
                }
            }
        }

        private string _spotifyCurrentUrl = string.Empty;
        public string SpotifyCurrentUrl
        {
            get => string.IsNullOrEmpty(_spotifyCurrentUrl) ? SpotifyPlaylistUrl : _spotifyCurrentUrl;
            set => SetProperty(ref _spotifyCurrentUrl, value);
        }

        private string FormatSpotifyUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            if (url.Contains("/embed/")) return url;
            return url.Replace("open.spotify.com/", "open.spotify.com/embed/");
        }

        public string InAppNotificationMessage
        {
            get => _inAppNotificationMessage;
            set => SetProperty(ref _inAppNotificationMessage, value);
        }

        public bool IsInAppNotificationVisible
        {
            get => _isInAppNotificationVisible;
            set => SetProperty(ref _isInAppNotificationVisible, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        // Strict Mode Properties
        public bool IsStrictModeWarningVisible
        {
            get => _isStrictModeWarningVisible;
            set => SetProperty(ref _isStrictModeWarningVisible, value);
        }

        public int StrictModeCountdown
        {
            get => _strictModeCountdown;
            set => SetProperty(ref _strictModeCountdown, value);
        }

        // Advanced Trello-like Properties
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    ApplyFilters();
                }
            }
        }

        public string MultiLinePasteText
        {
            get => _multiLinePasteText;
            set => SetProperty(ref _multiLinePasteText, value);
        }

        public bool IsMultiLinePastePromptVisible
        {
            get => _isMultiLinePastePromptVisible;
            set => SetProperty(ref _isMultiLinePastePromptVisible, value);
        }

        public string SelectedThemeName
        {
            get => _selectedThemeName;
            set
            {
                if (SetProperty(ref _selectedThemeName, value))
                {
                    _settings.SelectedThemeName = value;
                    _dataService.SaveSettings(_settings);
                    UpdateBackgroundBrush();
                    OnPropertyChanged(nameof(IsCustomBackgroundThemeSelected));
                }
            }
        }

        public bool IsCustomBackgroundThemeSelected
        {
            get => SelectedThemeName == "Custom Image";
        }

        public string BackgroundStretchMode
        {
            get => _settings.BackgroundStretchMode;
            set
            {
                if (_settings.BackgroundStretchMode != value)
                {
                    _settings.BackgroundStretchMode = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(BackgroundStretchMode));
                    OnPropertyChanged(nameof(BackgroundStretch));
                    OnPropertyChanged(nameof(BackgroundZoomX));
                    OnPropertyChanged(nameof(BackgroundZoomY));
                    OnPropertyChanged(nameof(IsCustomZoomVisible));
                }
            }
        }

        public double BackgroundCustomZoom
        {
            get => _settings.BackgroundCustomZoom;
            set
            {
                if (Math.Abs(_settings.BackgroundCustomZoom - value) > 0.01)
                {
                    _settings.BackgroundCustomZoom = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(BackgroundCustomZoom));
                    OnPropertyChanged(nameof(BackgroundZoomX));
                    OnPropertyChanged(nameof(BackgroundZoomY));
                }
            }
        }

        public Stretch BackgroundStretch
        {
            get
            {
                return _settings.BackgroundStretchMode switch
                {
                    "Lấp đầy" => Stretch.Fill,
                    "Center" => Stretch.None,
                    "Stretch" => Stretch.Uniform,
                    "Tuỳ ý" => Stretch.UniformToFill, // Use UniformToFill as base stretch so that custom zooms and translates are consistent across all image resolutions
                    _ => Stretch.UniformToFill
                };
            }
        }

        public double BackgroundZoomX
        {
            get => _settings.BackgroundStretchMode == "Tuỳ ý" ? (_settings.BackgroundPhong / 100.0) * (_settings.BackgroundLat ? -1.0 : 1.0) : 1.0;
        }

        public double BackgroundZoomY
        {
            get => _settings.BackgroundStretchMode == "Tuỳ ý" ? (_settings.BackgroundPhong / 100.0) : 1.0;
        }

        public double BackgroundOffsetX
        {
            get => _settings.BackgroundStretchMode == "Tuỳ ý" ? (_settings.BackgroundNgang - 50) * 8 : 0;
        }

        public double BackgroundOffsetY
        {
            get => _settings.BackgroundStretchMode == "Tuỳ ý" ? (_settings.BackgroundDoc - 50) * 8 : 0;
        }

        public bool IsCustomZoomVisible
        {
            get => _settings.BackgroundStretchMode == "Tuỳ ý";
        }

        private string _wallpaperEngineSourceDirectory = string.Empty;
        public string WallpaperEngineSourceDirectory
        {
            get => _wallpaperEngineSourceDirectory;
            set => SetProperty(ref _wallpaperEngineSourceDirectory, value);
        }

        public double BackgroundNgang
        {
            get => _settings.BackgroundNgang;
            set
            {
                if (Math.Abs(_settings.BackgroundNgang - value) > 0.1)
                {
                    _settings.BackgroundNgang = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(BackgroundNgang));
                    OnPropertyChanged(nameof(BackgroundOffsetX));
                }
            }
        }

        public double BackgroundDoc
        {
            get => _settings.BackgroundDoc;
            set
            {
                if (Math.Abs(_settings.BackgroundDoc - value) > 0.1)
                {
                    _settings.BackgroundDoc = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(BackgroundDoc));
                    OnPropertyChanged(nameof(BackgroundOffsetY));
                }
            }
        }

        public double BackgroundPhong
        {
            get => _settings.BackgroundPhong;
            set
            {
                if (Math.Abs(_settings.BackgroundPhong - value) > 0.1)
                {
                    _settings.BackgroundPhong = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(BackgroundPhong));
                    OnPropertyChanged(nameof(BackgroundZoomX));
                    OnPropertyChanged(nameof(BackgroundZoomY));
                }
            }
        }

        public bool BackgroundLat
        {
            get => _settings.BackgroundLat;
            set
            {
                if (_settings.BackgroundLat != value)
                {
                    _settings.BackgroundLat = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(BackgroundLat));
                    OnPropertyChanged(nameof(BackgroundZoomX));
                }
            }
        }

        public bool ShowColorCustom
        {
            get => _settings.ShowColorCustom;
            set
            {
                if (_settings.ShowColorCustom != value)
                {
                    _settings.ShowColorCustom = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(ShowColorCustom));
                }
            }
        }

        public double BackgroundBrightness
        {
            get => _settings.BackgroundBrightness;
            set
            {
                if (Math.Abs(_settings.BackgroundBrightness - value) > 0.1)
                {
                    _settings.BackgroundBrightness = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(BackgroundBrightness));
                    OnPropertyChanged(nameof(BackgroundBrightnessOverlayColor));
                    OnPropertyChanged(nameof(BackgroundBrightnessOverlayOpacity));
                }
            }
        }

        public double BackgroundContrast
        {
            get => _settings.BackgroundContrast;
            set
            {
                if (Math.Abs(_settings.BackgroundContrast - value) > 0.1)
                {
                    _settings.BackgroundContrast = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(BackgroundContrast));
                    OnPropertyChanged(nameof(BackgroundContrastOverlayOpacity));
                }
            }
        }

        public double BackgroundSaturation
        {
            get => _settings.BackgroundSaturation;
            set
            {
                if (Math.Abs(_settings.BackgroundSaturation - value) > 0.1)
                {
                    _settings.BackgroundSaturation = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(BackgroundSaturation));
                    OnPropertyChanged(nameof(BackgroundDesaturationOpacity));
                }
            }
        }

        public double BackgroundHueShift
        {
            get => _settings.BackgroundHueShift;
            set
            {
                if (Math.Abs(_settings.BackgroundHueShift - value) > 0.1)
                {
                    _settings.BackgroundHueShift = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(BackgroundHueShift));
                    OnPropertyChanged(nameof(BackgroundTintBrush));
                    OnPropertyChanged(nameof(BackgroundTintOpacity));
                }
            }
        }

        public double BackgroundVideoVolume
        {
            get => _settings.BackgroundVideoVolume;
            set
            {
                if (Math.Abs(_settings.BackgroundVideoVolume - value) > 0.1)
                {
                    _settings.BackgroundVideoVolume = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged(nameof(BackgroundVideoVolume));
                    OnPropertyChanged(nameof(BackgroundVideoVolumeNormalized));
                }
            }
        }

        public double BackgroundVideoVolumeNormalized
        {
            get => _settings.BackgroundVideoVolume / 100.0;
        }

        // Computed values for overlays
        public double BackgroundDesaturationOpacity
        {
            get => (100.0 - _settings.BackgroundSaturation) / 100.0 * 0.9;
        }

        public Brush BackgroundBrightnessOverlayColor
        {
            get => _settings.BackgroundBrightness > 50 ? Brushes.White : Brushes.Black;
        }

        public double BackgroundBrightnessOverlayOpacity
        {
            get => Math.Abs(_settings.BackgroundBrightness - 50) / 50.0 * 0.8;
        }

        public double BackgroundContrastOverlayOpacity
        {
            get => Math.Abs(_settings.BackgroundContrast - 50) / 50.0 * 0.4;
        }

        public Brush BackgroundTintBrush
        {
            get
            {
                double h = _settings.BackgroundHueShift / 100.0;
                // Generate a vibrant color from the hue value
                Color c = ColorFromHslHelper(h, 0.8, 0.5);
                return new SolidColorBrush(c);
            }
        }

        public double BackgroundTintOpacity
        {
            get => _settings.BackgroundHueShift == 50 ? 0.0 : 0.25;
        }

        private static Color ColorFromHslHelper(double h, double s, double l)
        {
            double r, g, b;
            if (s == 0)
            {
                r = g = b = l;
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

        public Brush SelectedBackgroundBrush
        {
            get => _selectedBackgroundBrush;
            set
            {
                if (SetProperty(ref _selectedBackgroundBrush, value))
                {
                    // Update MainWindowBackgroundBrush if it is not currently transparent (which is set by HTML wallpaper)
                    if (MainWindowBackgroundBrush != Brushes.Transparent)
                    {
                        MainWindowBackgroundBrush = value;
                    }
                }
            }
        }

        private Brush _mainWindowBackgroundBrush = Brushes.Black;
        public Brush MainWindowBackgroundBrush
        {
            get => _mainWindowBackgroundBrush;
            set => SetProperty(ref _mainWindowBackgroundBrush, value);
        }

        public Brush SelectedAccentBrush
        {
            get => _selectedAccentBrush;
            set => SetProperty(ref _selectedAccentBrush, value);
        }

        public Brush CardBackgroundBrush
        {
            get => _cardBackgroundBrush;
            set => SetProperty(ref _cardBackgroundBrush, value);
        }

        public Brush CardBorderBrush
        {
            get => _cardBorderBrush;
            set => SetProperty(ref _cardBorderBrush, value);
        }

        public Brush TextForegroundBrush
        {
            get => _textForegroundBrush;
            set => SetProperty(ref _textForegroundBrush, value);
        }

        public Brush SubTextForegroundBrush
        {
            get => _subTextForegroundBrush;
            set => SetProperty(ref _subTextForegroundBrush, value);
        }

        public Brush TextBoxBackgroundBrush
        {
            get => _textBoxBackgroundBrush;
            set => SetProperty(ref _textBoxBackgroundBrush, value);
        }

        // Form Fields
        public string NewTaskTitle
        {
            get => _newTaskTitle;
            set => SetProperty(ref _newTaskTitle, value);
        }

        public string NewTaskPriority
        {
            get => _newTaskPriority;
            set => SetProperty(ref _newTaskPriority, value);
        }

        public int NewTaskEstimate
        {
            get => _newTaskEstimate;
            set => SetProperty(ref _newTaskEstimate, value);
        }

        public string NewTaskTag
        {
            get => _newTaskTag;
            set => SetProperty(ref _newTaskTag, value);
        }

        // Ambient Sound Controls
        public double AmbientVolume
        {
            get => _settings.BackgroundSoundVolume;
            set
            {
                if (_settings.BackgroundSoundVolume != value)
                {
                    _settings.BackgroundSoundVolume = value;
                    _audioService.SetAmbientVolume(value);
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedAmbientSound
        {
            get => _settings.SelectedBackgroundSound;
            set
            {
                if (_settings.SelectedBackgroundSound != value)
                {
                    _settings.SelectedBackgroundSound = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged();
                    
                    // Update ambient playback state if running in Focus Mode
                    if (IsRunning && TimerMode == "Focus")
                    {
                        _ = _audioService.StartAmbientAsync(value, AmbientVolume);
                    }
                    else
                    {
                        _audioService.StopAmbient();
                    }
                }
            }
        }

        public string SelectedAlarmSound
        {
            get => _settings.SelectedAlarmSound;
            set
            {
                if (_settings.SelectedAlarmSound != value)
                {
                    _settings.SelectedAlarmSound = value;
                    _dataService.SaveSettings(_settings);
                    OnPropertyChanged();
                }
            }
        }

        // Statistics Properties
        public int StatsCompletedToday
        {
            get => _statsCompletedToday;
            set => SetProperty(ref _statsCompletedToday, value);
        }

        public int StatsCompletedThisWeek
        {
            get => _statsCompletedThisWeek;
            set => SetProperty(ref _statsCompletedThisWeek, value);
        }

        public double StatsTaskCompletionRate
        {
            get => _statsTaskCompletionRate;
            set => SetProperty(ref _statsTaskCompletionRate, value);
        }

        public List<TagStatItem> StatsTagAllocation
        {
            get => _statsTagAllocation;
            set => SetProperty(ref _statsTagAllocation, value);
        }

        #endregion

        #region Commands

        public ICommand AddTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand ToggleCompleteTaskCommand { get; }
        public ICommand SelectFocusTaskCommand { get; }
        
        public ICommand StartTimerCommand { get; }
        public ICommand PauseTimerCommand { get; }
        public ICommand ResetTimerCommand { get; }
        
        public ICommand SwitchModeCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand ToggleSettingsCommand { get; }
        public ICommand CloseNotificationCommand { get; }
        public ICommand ToggleSpotifyPlayerCommand { get; }
        public ICommand NavigateToSpotifyLoginCommand { get; }
        public ICommand NavigateToSpotifyPlayerCommand { get; }

        // Advanced Commands
        public ICommand CloneTaskCommand { get; }
        public ICommand ToggleWatchTaskCommand { get; }
        public ICommand OpenAttachmentCommand { get; }
        public ICommand AddAttachmentCommand { get; }
        public ICommand RemoveAttachmentCommand { get; }

        public ICommand SplitMultiLinePasteCommand { get; }
        public ICommand AddMultiLinePasteAsSingleCommand { get; }
        public ICommand SelectThemeCommand { get; }
        public ICommand ChooseCustomImageCommand { get; }

        #endregion

        #region Task Management Methods

        private void AddTask()
        {
            if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;

            // Detect multi-line text input
            if (NewTaskTitle.Contains("\r") || NewTaskTitle.Contains("\n"))
            {
                MultiLinePasteText = NewTaskTitle;
                IsMultiLinePastePromptVisible = true;
                return;
            }

            var task = new TodoTask
            {
                Title = NewTaskTitle.Trim(),
                Priority = NewTaskPriority,
                EstimatedPomodoros = NewTaskEstimate,
                Tag = NewTaskTag,
                IsCompleted = false
            };

            _allTasks.Add(task);
            
            // Set as active if none is active
            if (ActiveFocusTask == null)
            {
                ActiveFocusTask = task;
            }

            // Clear inputs
            NewTaskTitle = string.Empty;
            NewTaskPriority = "Medium";
            NewTaskEstimate = 1;
            NewTaskTag = "Work";

            SaveAndApplyFilters();
            UpdateStatistics();
        }

        private void SplitMultiLinePaste()
        {
            var lines = MultiLinePasteText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            TodoTask? firstNewTask = null;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var task = new TodoTask
                {
                    Title = line.Trim(),
                    Priority = NewTaskPriority,
                    EstimatedPomodoros = NewTaskEstimate,
                    Tag = NewTaskTag
                };
                _allTasks.Add(task);
                if (firstNewTask == null) firstNewTask = task;
            }

            if (ActiveFocusTask == null && firstNewTask != null)
            {
                ActiveFocusTask = firstNewTask;
            }

            IsMultiLinePastePromptVisible = false;
            NewTaskTitle = string.Empty;
            SaveAndApplyFilters();
            UpdateStatistics();
        }

        private void AddMultiLinePasteAsSingle()
        {
            var task = new TodoTask
            {
                Title = MultiLinePasteText.Trim(),
                Priority = NewTaskPriority,
                EstimatedPomodoros = NewTaskEstimate,
                Tag = NewTaskTag
            };
            _allTasks.Add(task);

            if (ActiveFocusTask == null)
            {
                ActiveFocusTask = task;
            }

            IsMultiLinePastePromptVisible = false;
            NewTaskTitle = string.Empty;
            SaveAndApplyFilters();
            UpdateStatistics();
        }

        private void DeleteTask(TodoTask task)
        {
            if (task == null) return;
            _allTasks.Remove(task);

            if (ActiveFocusTask == task)
            {
                ActiveFocusTask = _allTasks.FirstOrDefault(t => !t.IsCompleted);
            }

            SaveAndApplyFilters();
            UpdateStatistics();
        }

        private void ToggleCompleteTask(TodoTask task)
        {
            if (task == null) return;
            task.IsCompleted = !task.IsCompleted;
            task.CompletedAt = task.IsCompleted ? DateTime.Now : null;
            
            SaveAndApplyFilters();
            UpdateStatistics();
        }

        private void SelectFocusTask(TodoTask task)
        {
            if (task == null) return;
            ActiveFocusTask = task;
        }

        private void CloneTask(TodoTask task)
        {
            if (task == null) return;
            var clone = new TodoTask
            {
                Title = task.Title + " (Bản sao)",
                Priority = task.Priority,
                EstimatedPomodoros = task.EstimatedPomodoros,
                Tag = task.Tag,
                Attachments = new List<string>(task.Attachments)
            };
            _allTasks.Add(clone);
            SaveAndApplyFilters();
        }

        private void ToggleWatchTask(TodoTask task)
        {
            if (task == null) return;
            task.IsWatched = !task.IsWatched;
            SaveAndApplyFilters();
        }

        private void AddAttachment(object parameter)
        {
            if (parameter is object[] args && args.Length == 2 && args[0] is TodoTask task && args[1] is string filePath)
            {
                if (task.Attachments == null) task.Attachments = new();
                if (!task.Attachments.Contains(filePath))
                {
                    task.Attachments.Add(filePath);
                    SaveAndApplyFilters();
                }
            }
        }

        private void RemoveAttachment(object parameter)
        {
            if (parameter is object[] args && args.Length == 2 && args[0] is TodoTask task && args[1] is string filePath)
            {
                if (task.Attachments != null && task.Attachments.Contains(filePath))
                {
                    task.Attachments.Remove(filePath);
                    SaveAndApplyFilters();
                }
            }
        }

        private void OpenAttachment(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                InAppNotificationMessage = $"Không thể mở tệp đính kèm: {ex.Message}";
                IsInAppNotificationVisible = true;
            }
        }

        private void SelectTheme(string themeName)
        {
            SelectedThemeName = themeName;
        }

        private void ResetWallpaperAlignmentSettings()
        {
            _settings.BackgroundStretchMode = "Cover";
            _settings.BackgroundCustomZoom = 1.0;
            _settings.BackgroundNgang = 50;
            _settings.BackgroundDoc = 50;
            _settings.BackgroundPhong = 100;
            _settings.BackgroundLat = false;
            _settings.ShowColorCustom = false;
            _settings.BackgroundBrightness = 50;
            _settings.BackgroundContrast = 50;
            _settings.BackgroundSaturation = 100;
            _settings.BackgroundHueShift = 50;
            _settings.BackgroundVideoVolume = 0.0;

            OnPropertyChanged(nameof(BackgroundStretchMode));
            OnPropertyChanged(nameof(BackgroundStretch));
            OnPropertyChanged(nameof(BackgroundCustomZoom));
            OnPropertyChanged(nameof(BackgroundNgang));
            OnPropertyChanged(nameof(BackgroundDoc));
            OnPropertyChanged(nameof(BackgroundPhong));
            OnPropertyChanged(nameof(BackgroundLat));
            OnPropertyChanged(nameof(BackgroundZoomX));
            OnPropertyChanged(nameof(BackgroundZoomY));
            OnPropertyChanged(nameof(BackgroundOffsetX));
            OnPropertyChanged(nameof(BackgroundOffsetY));
            OnPropertyChanged(nameof(IsCustomZoomVisible));
            OnPropertyChanged(nameof(ShowColorCustom));
            OnPropertyChanged(nameof(BackgroundBrightness));
            OnPropertyChanged(nameof(BackgroundContrast));
            OnPropertyChanged(nameof(BackgroundSaturation));
            OnPropertyChanged(nameof(BackgroundHueShift));
            OnPropertyChanged(nameof(BackgroundVideoVolume));
            OnPropertyChanged(nameof(BackgroundVideoVolumeNormalized));
        }

        private void ChooseCustomImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Media & Web Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.wmv;*.avi;*.mov;*.mkv;*.html;*.htm",
                Title = "Chọn ảnh/video/gif/html nền tùy chỉnh"
            };

            if (dlg.ShowDialog() == true)
            {
                // Reset all old alignment and color tuning parameters to prevent image distortion on new loads
                ResetWallpaperAlignmentSettings();

                Settings.CustomBackgroundImagePath = dlg.FileName;
                _dataService.SaveSettings(Settings);
                
                if (SelectedThemeName == "Custom Image")
                {
                    UpdateBackgroundBrush();
                    OnPropertyChanged(nameof(Settings));
                }
                else
                {
                    SelectedThemeName = "Custom Image";
                }
            }
        }

        private void SaveAndApplyFilters()
        {
            _dataService.SaveTasks(_allTasks);
            ApplyFilters();
        }

        public void SaveSettingsOnClose()
        {
            _dataService.SaveSettings(_settings);
            _discordRpcService.Deinitialize();
        }

        public void ApplyFilters()
        {
            var query = SearchQuery?.Trim() ?? string.Empty;
            IEnumerable<TodoTask> filtered = _allTasks;

            // Filter operators
            if (!string.IsNullOrEmpty(query))
            {
                var words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    if (word.Equals("has:attachments", StringComparison.OrdinalIgnoreCase))
                    {
                        filtered = filtered.Where(t => t.Attachments != null && t.Attachments.Count > 0);
                    }
                    else if (word.StartsWith("priority:", StringComparison.OrdinalIgnoreCase) && word.Length > 9)
                    {
                        var priority = word.Substring(9);
                        filtered = filtered.Where(t => t.Priority.Equals(priority, StringComparison.OrdinalIgnoreCase));
                    }
                    else if (word.StartsWith("tag:", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
                    {
                        var tag = word.Substring(4);
                        filtered = filtered.Where(t => t.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
                    }
                    else if (word.Equals("is:completed", StringComparison.OrdinalIgnoreCase))
                    {
                        filtered = filtered.Where(t => t.IsCompleted);
                    }
                    else if (word.Equals("is:watched", StringComparison.OrdinalIgnoreCase))
                    {
                        filtered = filtered.Where(t => t.IsWatched);
                    }
                    else
                    {
                        // Match title
                        filtered = filtered.Where(t => t.Title.Contains(word, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }

            // Watch/Pin float to top, then sorted by CreatedTime
            var sorted = filtered
                .OrderByDescending(t => t.IsWatched)
                .ThenBy(t => t.CreatedAt)
                .ToList();

            Tasks.Clear();
            foreach (var t in sorted)
            {
                Tasks.Add(t);
            }
        }

        #endregion

        #region Timer Core Logic

        private void ResetTimerValues()
        {
            int minutes = TimerMode switch
            {
                "Focus" => Settings.FocusDurationMinutes,
                "Short Break" => Settings.ShortBreakDurationMinutes,
                "Long Break" => Settings.LongBreakDurationMinutes,
                _ => 25
            };

            _timeLeftSeconds = minutes * 60;
            _totalSessionDurationSeconds = _timeLeftSeconds;
            ProgressValue = 1.0;
            UpdateTimerText();
        }

        private void UpdateTimerText()
        {
            int minutes = _timeLeftSeconds / 60;
            int seconds = _timeLeftSeconds % 60;
            TimerText = $"{minutes:D2}:{seconds:D2}";
        }

        private void UpdateDiscordPresence()
        {
            _discordRpcService.UpdatePresence(
                _timerMode,
                _activeFocusTask?.Title,
                _isRunning,
                _timeLeftSeconds,
                _totalSessionDurationSeconds
            );
        }

        private void StartTimer()
        {
            if (IsRunning) return;

            IsRunning = true;
            _timer.Start();

            // Ambient background sounds in Focus mode only
            if (TimerMode == "Focus")
            {
                _ = _audioService.StartAmbientAsync(SelectedAmbientSound, AmbientVolume);
                
                // Reset warning states
                IsStrictModeWarningVisible = false;
                _strictModeCountdown = 5;
            }

            UpdateDiscordPresence();
        }

        private void PauseTimer()
        {
            if (!IsRunning) return;

            IsRunning = false;
            _timer.Stop();
            _strictModeTimer.Stop();
            _audioService.StopAmbient();
            IsStrictModeWarningVisible = false;

            UpdateDiscordPresence();
        }

        private void ResetTimer()
        {
            // Reset/Cancel wipes session progress, does NOT record to history
            PauseTimer();
            ResetTimerValues();
            UpdateDiscordPresence();
        }

        private void SwitchMode(string mode)
        {
            PauseTimer();
            TimerMode = mode;
            ResetTimerValues();
            UpdateDiscordPresence();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_timeLeftSeconds > 0)
            {
                _timeLeftSeconds--;
                ProgressValue = (double)_timeLeftSeconds / _totalSessionDurationSeconds;
                UpdateTimerText();
            }
            else
            {
                CompleteSession();
            }
        }

        private void CompleteSession()
        {
            PauseTimer();
            _audioService.PlayAlarm(Settings.SelectedAlarmSound);

            try
            {
                if (TimerMode == "Focus")
                {
                    new Microsoft.Toolkit.Uwp.Notifications.ToastContentBuilder()
                        .AddText("Hoàn thành tập trung! 🎯")
                        .AddText(ActiveFocusTask != null ? $"Bạn đã hoàn thành phiên tập trung cho nhiệm vụ: {ActiveFocusTask.Title}." : "Đã hoàn thành một phiên tập trung thành công. Hãy nghỉ ngơi!")
                        .Show();
                }
                else
                {
                    new Microsoft.Toolkit.Uwp.Notifications.ToastContentBuilder()
                        .AddText("Hết giờ nghỉ ngơi! ⚡")
                        .AddText("Đã đến lúc quay lại tập trung làm việc rồi.")
                        .Show();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show toast notification: {ex.Message}");
            }

            if (TimerMode == "Focus")
            {
                // Record session
                var session = new PomodoroSession
                {
                    StartTime = DateTime.Now.AddSeconds(-_totalSessionDurationSeconds),
                    EndTime = DateTime.Now,
                    DurationMinutes = (double)_totalSessionDurationSeconds / 60.0,
                    IsCompleted = true,
                    Tag = ActiveFocusTask?.Tag ?? "Work",
                    TaskId = ActiveFocusTask?.Id,
                    TaskTitle = ActiveFocusTask?.Title ?? "General Focus"
                };

                // Increment completed pomodoro count on task
                if (ActiveFocusTask != null)
                {
                    ActiveFocusTask.CompletedPomodoros++;
                    _dataService.SaveTasks(_allTasks);
                }

                var history = _dataService.GetHistory();
                history.Add(session);
                _dataService.SaveHistory(history);
                UpdateStatistics();
            }

            // Transition automatically if enabled
            if (Settings.AutoStartNextSession)
            {
                if (TimerMode == "Focus")
                {
                    SwitchMode("Short Break");
                }
                else
                {
                    SwitchMode("Focus");
                }
                StartTimer();
            }
            else
            {
                ResetTimerValues();
            }
        }

        #endregion

        #region Focus Event Hooks

        public void OnAppActivated()
        {
            if (_strictModeTimer.IsEnabled)
            {
                _strictModeTimer.Stop();
            }
            IsStrictModeWarningVisible = false;
            StrictModeCountdown = 5;
        }

        public void OnAppDeactivated()
        {
            if (IsRunning && TimerMode == "Focus" && Settings.IsStrictModeEnabled)
            {
                if (!IsStrictModeWarningVisible)
                {
                    IsStrictModeWarningVisible = true;
                    StrictModeCountdown = 5;
                    _strictModeTimer.Start();
                }
            }
        }

        private void StrictModeTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsRunning || TimerMode != "Focus" || !Settings.IsStrictModeEnabled)
            {
                _strictModeTimer.Stop();
                IsStrictModeWarningVisible = false;
                return;
            }

            StrictModeCountdown--;
            System.Media.SystemSounds.Beep.Play();

            if (StrictModeCountdown <= 0)
            {
                _strictModeTimer.Stop();
                ResetTimer();
                IsStrictModeWarningVisible = false;
                InAppNotificationMessage = "Phiên tập trung đã bị HỦY vì bạn rời khỏi ứng dụng trong chế độ Strict Mode!";
                IsInAppNotificationVisible = true;
            }
        }

        #endregion

        #region Settings Management

        private void SaveSettings()
        {
            _dataService.SaveSettings(Settings);

            // Configure Startup Registry Key for automatic boot launch
            try
            {
                string appName = "DuckPomodoro";
                string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, true))
                {
                    if (key != null)
                    {
                        if (Settings.IsAutoStartWithWindows)
                        {
                            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                            if (!string.IsNullOrEmpty(exePath))
                            {
                                key.SetValue(appName, $"\"{exePath}\"");
                            }
                        }
                        else
                        {
                            key.DeleteValue(appName, false);
                        }
                    }
                }
            }
            catch
            {
                // Silent fallback if permission issue
            }

            if (!IsRunning)
            {
                ResetTimerValues();
            }
        }

        #endregion

        #region Statistics Processing

        private void UpdateStatistics()
        {
            var history = _dataService.GetHistory();
            var now = DateTime.Now;
            var today = now.Date;
            
            // Start of week (Monday)
            int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek = now.AddDays(-1 * diff).Date;

            // 1. Completed Today
            StatsCompletedToday = history.Count(s => s.StartTime.Date == today && s.IsCompleted);

            // 2. Completed This Week
            StatsCompletedThisWeek = history.Count(s => s.StartTime.Date >= startOfWeek && s.IsCompleted);

            // 3. Task Completion Rate
            int totalTasks = _allTasks.Count;
            int completedTasks = _allTasks.Count(t => t.IsCompleted);
            StatsTaskCompletionRate = totalTasks > 0 ? (double)completedTasks / totalTasks * 100.0 : 0.0;

            // 4. Allocation by tags
            var tagDuration = new Dictionary<string, double>();
            foreach (var session in history)
            {
                string tag = string.IsNullOrWhiteSpace(session.Tag) ? "General" : session.Tag;
                if (!tagDuration.ContainsKey(tag))
                {
                    tagDuration[tag] = 0;
                }
                tagDuration[tag] += session.DurationMinutes;
            }

            var colorPalettes = new[] { "#FF7043", "#42A5F5", "#66BB6A", "#AB47BC", "#FFCA28", "#26A69A" };
            int colorIdx = 0;

            StatsTagAllocation = tagDuration.Select(kv => new TagStatItem
            {
                TagName = kv.Key,
                Minutes = Math.Round(kv.Value, 1),
                Color = colorPalettes[colorIdx++ % colorPalettes.Length]
            }).ToList();
        }

        #endregion

        #region Background Themes Helper

        private void UpdateBackgroundBrush()
        {
            var accentColorConverter = new BrushConverter();

            // Set default dark values with softer semi-transparent border and card background (70% opacity glassmorphism)
            CardBackgroundBrush = new SolidColorBrush(Color.FromArgb(180, 18, 18, 20));
            CardBorderBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)); // Softer border (8% opacity white)
            TextForegroundBrush = new SolidColorBrush(Color.FromRgb(244, 244, 245));
            SubTextForegroundBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
            TextBoxBackgroundBrush = new SolidColorBrush(Color.FromArgb(200, 24, 24, 27));

            switch (SelectedThemeName)
            {
                case "Cosmic Blue":
                    {
                        var brush = new LinearGradientBrush();
                        brush.StartPoint = new Point(0, 0);
                        brush.EndPoint = new Point(1, 1);
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(2, 2, 5), 0.0));
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(11, 19, 43), 1.0));
                        SelectedBackgroundBrush = brush;
                        SelectedAccentBrush = (Brush)accentColorConverter.ConvertFromString("#64B5F6")!;
                    }
                    break;
                case "Emerald Aurora":
                    {
                        var brush = new LinearGradientBrush();
                        brush.StartPoint = new Point(0, 0);
                        brush.EndPoint = new Point(1, 1);
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(5, 9, 5), 0.0));
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(10, 47, 29), 1.0));
                        SelectedBackgroundBrush = brush;
                        SelectedAccentBrush = (Brush)accentColorConverter.ConvertFromString("#81C784")!;
                    }
                    break;
                case "Sunset Amber":
                    {
                        var brush = new LinearGradientBrush();
                        brush.StartPoint = new Point(0, 0);
                        brush.EndPoint = new Point(1, 1);
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(10, 5, 5), 0.0));
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(44, 17, 17), 1.0));
                        SelectedBackgroundBrush = brush;
                        SelectedAccentBrush = (Brush)accentColorConverter.ConvertFromString("#FFB74D")!;
                    }
                    break;
                case "Midnight Purple":
                    {
                        var brush = new LinearGradientBrush();
                        brush.StartPoint = new Point(0, 0);
                        brush.EndPoint = new Point(1, 1);
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(9, 5, 15), 0.0));
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(29, 11, 46), 1.0));
                        SelectedBackgroundBrush = brush;
                        SelectedAccentBrush = (Brush)accentColorConverter.ConvertFromString("#BA68C8")!;
                    }
                    break;
                case "Forest Moss":
                    {
                        var brush = new LinearGradientBrush();
                        brush.StartPoint = new Point(0, 0);
                        brush.EndPoint = new Point(1, 1);
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(4, 8, 4), 0.0));
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(27, 46, 27), 1.0));
                        SelectedBackgroundBrush = brush;
                        SelectedAccentBrush = (Brush)accentColorConverter.ConvertFromString("#C5E1A5")!;
                    }
                    break;
                case "Cyberpunk Neon":
                    {
                        var brush = new LinearGradientBrush();
                        brush.StartPoint = new Point(0, 0);
                        brush.EndPoint = new Point(1, 1);
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(2, 2, 10), 0.0));
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(21, 5, 39), 1.0));
                        SelectedBackgroundBrush = brush;
                        SelectedAccentBrush = (Brush)accentColorConverter.ConvertFromString("#F48FB1")!;
                    }
                    break;
                case "Ocean Depth":
                    {
                        var brush = new LinearGradientBrush();
                        brush.StartPoint = new Point(0, 0);
                        brush.EndPoint = new Point(1, 1);
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(1, 6, 15), 0.0));
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(5, 32, 58), 1.0));
                        SelectedBackgroundBrush = brush;
                        SelectedAccentBrush = (Brush)accentColorConverter.ConvertFromString("#81D4FA")!;
                    }
                    break;
                case "Sakura Dusk":
                    {
                        var brush = new LinearGradientBrush();
                        brush.StartPoint = new Point(0, 0);
                        brush.EndPoint = new Point(1, 1);
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(13, 5, 7), 0.0));
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(44, 17, 24), 1.0));
                        SelectedBackgroundBrush = brush;
                        SelectedAccentBrush = (Brush)accentColorConverter.ConvertFromString("#F48FB1")!;
                    }
                    break;
                case "Carbon Gray":
                    SelectedBackgroundBrush = new SolidColorBrush(Color.FromRgb(28, 28, 30));
                    SelectedAccentBrush = (Brush)accentColorConverter.ConvertFromString("#80DEEA")!;
                    break;
                case "Frost White": // PREMIUM LIGHT MODE
                    {
                        var brush = new LinearGradientBrush();
                        brush.StartPoint = new Point(0, 0);
                        brush.EndPoint = new Point(1, 1);
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(243, 244, 246), 0.0)); // Light grey/white
                        brush.GradientStops.Add(new GradientStop(Color.FromRgb(229, 231, 235), 1.0));
                        SelectedBackgroundBrush = brush;
                        SelectedAccentBrush = (Brush)accentColorConverter.ConvertFromString("#1F6C9F")!; // Slate Blue

                        // Set light theme element brushes with softer border and card background (70% opacity glassmorphism)
                        CardBackgroundBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)); // Pure White with 70% opacity
                        CardBorderBrush = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)); // Softer border (10% opacity black)
                        TextForegroundBrush = new SolidColorBrush(Color.FromRgb(24, 24, 27)); // Near Black
                        SubTextForegroundBrush = new SolidColorBrush(Color.FromRgb(113, 113, 122)); // Charcoal Subtext
                        TextBoxBackgroundBrush = new SolidColorBrush(Color.FromArgb(200, 244, 244, 245)); // Light text box with 78% opacity
                    }
                    break;
                case "Custom Image":
                    SelectedBackgroundBrush = Brushes.Transparent;
                    SelectedAccentBrush = Brushes.Tomato;
                    break;
                case "Deep OLED":
                default:
                    SelectedBackgroundBrush = new SolidColorBrush(Color.FromRgb(5, 5, 5));
                    SelectedAccentBrush = Brushes.Tomato;
                    break;
            }

            MainWindowBackgroundBrush = SelectedBackgroundBrush;
        }

        #endregion
    }
}
