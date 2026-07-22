namespace Pomodoro.Models
{
    public class AppSettings
    {
        public int FocusDurationMinutes { get; set; } = 25;
        public int ShortBreakDurationMinutes { get; set; } = 5;
        public int LongBreakDurationMinutes { get; set; } = 15;
        public bool AutoStartNextSession { get; set; } = false;
        public bool IsStrictModeEnabled { get; set; } = false;
        public string SelectedBackgroundSound { get; set; } = "None"; // maps to Focus Sound: "None", "Ticking Fast", "Ticking Slow", "White Noise", "Brown Noise"
        public double BackgroundSoundVolume { get; set; } = 0.5; // 0.0 to 1.0
        public string SelectedThemeName { get; set; } = "Deep OLED";
        public bool IsAutoStartWithWindows { get; set; } = false;
        public string CustomBackgroundImagePath { get; set; } = string.Empty;
        
        // Alarm Sound selection
        public string SelectedAlarmSound { get; set; } = "Kitchen"; // "Bell", "Bird", "Digital", "Kitchen", "Wood"

        // Window size, position, and state persistence
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public double WindowWidth { get; set; } = 1280;
        public double WindowHeight { get; set; } = 720;
        public string WindowState { get; set; } = "Normal";
        public double TodoColumnWidth { get; set; } = -1;
        public string BackgroundStretchMode { get; set; } = "Cover";
        public double BackgroundCustomZoom { get; set; } = 1.0;
        
        // Custom Alignment & Zoom ("Tuỳ ý" properties)
        public double BackgroundNgang { get; set; } = 50;
        public double BackgroundDoc { get; set; } = 50;
        public double BackgroundPhong { get; set; } = 100;
        public bool BackgroundLat { get; set; } = false;

        // Color Customization properties
        public bool ShowColorCustom { get; set; } = false;
        public double BackgroundBrightness { get; set; } = 50;
        public double BackgroundContrast { get; set; } = 50;
        public double BackgroundSaturation { get; set; } = 100;
        public double BackgroundHueShift { get; set; } = 50;
        public double BackgroundVideoVolume { get; set; } = 0.0; // Range: 0 to 100 (WPF MediaElement uses 0 to 1.0, so we divide by 100)
        public string SpotifyPlaylistUrl { get; set; } = "https://open.spotify.com/embed/playlist/37i9dQZF1DWWQRwui0ExPn";
    }
}
