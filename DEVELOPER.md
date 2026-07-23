# Duck Pomodoro - Hướng dẫn Phát triển & Sửa lỗi (Developer Guide)

Tài liệu này tổng hợp toàn bộ kiến thức kiến trúc, thiết kế, và các giải pháp sửa lỗi đặc thù trong dự án **Duck Pomodoro** để hỗ trợ nhà phát triển (hoặc AI trợ lý) dễ dàng tiếp cận, bảo trì và sửa lỗi trong tương lai.

---

## 1. Yêu cầu Môi trường & Dependencies (Prerequisites)

Để biên dịch và đóng gói dự án từ zero (hoặc khi thiết lập GitHub Actions / CI Server):
* **SDK**: .NET SDK 10.0-windows
* **Trình đóng gói Installer**: Inno Setup 6 (`iscc`)
* **Công cụ CLI**: Git, GitHub CLI (`gh`)
* **Thư viện WPF & NuGet Dependencies**:
  * `DiscordRichPresence`: Đồng bộ trạng thái học tập/nghỉ ngơi lên Discord.
  * `Microsoft.Web.WebView2`: Trình phát hình nền HTML/Web Wallpaper Engine.
  * `System.Management`: Thư viện truy vấn hệ thống Windows.
  * `System.Windows.Forms` (Assembly reference): Truy vấn thông số màn hình `Screen.PrimaryScreen.Bounds`.

---

## 2. Kiến trúc Dự án (Project Architecture)

Dự án được xây dựng trên nền tảng **WPF (.NET 10.0-windows)** theo mô hình chuẩn **MVVM (Model-View-ViewModel)**:
* **Views (`MainWindow.xaml` / `MainWindow.xaml.cs`)**: Quản lý giao diện, vòng đời cửa sổ, các sự kiện chuột/bàn phím và các cấu hình đồ họa Win32 đặc thù (Window Chrome, Hooks).
* **ViewModels (`ViewModels\MainViewModel.cs`)**: Chứa logic nghiệp vụ của đồng hồ Pomodoro, quản lý danh sách công việc (Todo List), trạng thái cấu hình hệ thống, và liên kết dữ liệu (Data Binding).
* **Services (`Services\`)**:
  * `DataService.cs`: Đọc/ghi cấu hình người dùng và danh sách công việc xuống file JSON (`%LOCALAPPDATA%\PomodoroApp\app_data.json`).
  * `AudioService.cs`: Tạo âm thanh tích tắc nhịp nhàng và phát nhạc nền ambient bằng Win32 API waveOut.
  * `DiscordRpcService.cs`: Đồng bộ hóa trạng thái học tập/nghỉ ngơi lên Discord Rich Presence.
* **Installer Config (`installer.iss`)**: Kịch bản đóng gói ứng dụng thành file cài đặt Windows thông qua **Inno Setup 6**.

### Sơ đồ Cấu trúc Thư mục (Directory Tree)
```text
DuckPomodoro/
├── Models/
│   ├── AppSettings.cs
│   ├── TodoTask.cs
│   └── PomodoroSession.cs
├── Services/
│   ├── AudioService.cs
│   ├── DataService.cs
│   └── DiscordRpcService.cs
├── ViewModels/
│   └── MainViewModel.cs
├── Views/
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── StrictModeOverlayWindow.xaml
│   └── SpotifyWindow.xaml
├── installer.iss
├── DEVELOPER.md
└── .release_credentials.json (Ignored - Secrets)
```

---

## 3. Các giải pháp kỹ thuật đặc thù & Sửa lỗi (Critical Bug Fixes)

### A. Giao diện Kính mờ (Glassmorphism / Acrylic Custom Window)
* **Cơ chế**: Cửa sổ chính được thiết kế không viền hệ thống (`WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`).
* **Pha màu kính siêu trong suốt (Ultra-Translucent Glassmorphism < 20%)**:
  * Khi người dùng tải hình nền (Custom Image), thuật toán giải mã tự động downscale ảnh về $100 \times 100\text{ px}$ và phân tích mẫu màu bất đồng bộ (`Task.Run`).
  * Hạ mức Opacity của `CardBackgroundBrush` xuống **~18% Opacity (`45/255`)**, kết hợp đường viền kính vi-mảnh 1px màu trắng mờ (`rgba(255, 255, 255, 0.15)`). Giúp định hình các khung làm việc sắc nét mà không che phủ hình nền nghệ thuật bên dưới.
* **Tách chữ số Pomodoro trực tiếp lên Wallpaper**:
  * Loại bỏ hoàn toàn khối Card đục bao quanh đồng hồ. Đưa các chữ số thời gian `25:00` đứng 100% trực tiếp trên hình nền kết hợp `DropShadowEffect` (`BlurRadius=12, ShadowDepth=2, Opacity=0.75`).
  * Chỉ giữ lại các thanh điều khiển nhỏ gọn dạng viên thuốc (Pill bar) giúp khoảng giữa màn hình trống trải 100%.
* **FAB (Floating Action Button) & Modal Popup tạo nhiệm vụ**:
  * Ẩn hoàn toàn form nhập Task khỏi cột danh sách Todo. Thêm nút icon `+` Accent nổi ngay cạnh tiêu đề "Nhiệm vụ".
  * Khi click `+`, mở khung nhập Modal Glassmorphism nhỏ gọn ngay giữa màn hình. Khi gõ Enter hoặc bấm "Tạo thẻ", Modal sẽ tự động ẩn đi và đưa thẻ mới vào danh sách.
* **Sửa lỗi phóng to (Maximize) không lấp đầy và bị hở viền**:
  * **Giải pháp**: Nhờ có Win32 Hook `WM_GETMINMAXINFO` quản lý chính xác phạm vi làm việc của màn hình (`rcWorkArea`), chúng ta không cần co lề `8px` nữa (giữ `Margin = 0` cố định). Chỉ cần gọi `this.UpdateLayout()` trong sự kiện `StateChanged` để ép buộc WPF cập nhật toàn bộ khung bố trí và lấp đầy màn hình khi phóng to.

### B. Trình phát GIF động Native (Custom GIF Renderer)
* **Vấn đề**: `MediaElement` mặc định của WPF giải mã GIF rất kém, thường xuyên bị giật, nháy đen khung hình hoặc rò rỉ pixel trong suốt làm lộ IDE/Desktop phía sau.
* **Giải pháp**:
  * Sử dụng lớp **`GifBitmapDecoder`** để tách toàn bộ khung hình tĩnh của GIF và lấy thời gian trễ của từng khung.
  * Dùng **`RenderTargetBitmap`** kết hợp **`DrawingVisual`** tạo một canvas đệm tích lũy. Mỗi khung hình mới sẽ được vẽ đè lên các khung hình trước để giữ nguyên cấu trúc pixel trong suốt (Disposal Method: Combine).
  * Phát GIF thông qua đối tượng `Image` thông thường, loại bỏ hoàn toàn `MediaElement` đối với định dạng `.gif`.

### C. Cơ chế Maximize của Cửa sổ không viền (Win32 Interop Hook)
* Để ngăn cửa sổ không viền tràn đè lên thanh Taskbar khi phóng to, ứng dụng cài đặt một Win32 Hook lắng nghe thông điệp `WM_GETMINMAXINFO` (0x0024):
  ```csharp
  mmi.ptMaxPosition.X = rcWorkArea.Left;
  mmi.ptMaxPosition.Y = rcWorkArea.Top;
  mmi.ptMaxSize.X = rcWorkArea.Right - rcWorkArea.Left;
  mmi.ptMaxSize.Y = rcWorkArea.Bottom - rcWorkArea.Top;
  ```
  *Lưu ý*: Phải sử dụng tọa độ làm việc gốc `rcWorkArea` thay vì tính `Math.Abs` để hỗ trợ hiển thị hoàn hảo trên các thiết lập đa màn hình (Multi-Monitor) có tọa độ làm việc âm.

### E. Quy tắc thiết kế UI: Không xuất hiện viền nét đứt (No Dashed Focus Rectangle)
* **Yêu cầu thiết kế**: Tuyệt đối không để xuất hiện viền nét đứt (Focus Visual Style / Focus Rectangle) trên bất kỳ `Button` hay thành phần điều hướng/tương tác nào trong toàn bộ dự án để đảm bảo thẩm mỹ hiện đại.
* **Giải pháp**: Tất cả nút bấm và control đều phải được cấu hình `FocusVisualStyle="{x:Null}"` (trực tiếp trên phần tử hoặc thông qua `Style` chung).

### F. Cơ chế Strict Mode Fullscreen & Quản lý Taskbar Win32 (Strict Mode Fullscreen & Taskbar Management)
* **Cơ chế Fullscreen chuẩn Win32 & WPF**:
  * Khi bật Strict Mode Fullscreen (hoặc F11), ứng dụng tự động ẩn Custom Titlebar (`CustomTitleBar.Visibility = Visibility.Collapsed`) để nhường 100% diện tích chiều cao màn hình cho nội dung chính.
  * Khóa thuộc tính co giãn `this.ResizeMode = ResizeMode.NoResize` và `this.WindowStyle = WindowStyle.None` trước khi chuyển sang `this.WindowState = WindowState.Maximized` để triệt tiêu hoàn toàn viền co giãn ẩn của Windows DWM, ngăn ngừa hiện tượng nhấp nháy hoặc bị cản trở Fullscreen.
  * Tích hợp lớp **`TaskbarManager`** sử dụng Win32 API (`FindWindow("Shell_TrayWnd", "")` & `ShowWindow(hwnd, SW_HIDE/SW_SHOW)`) để ẩn vật lý thanh Taskbar của Windows khi ở Strict Mode và khôi phục lại thanh Taskbar an toàn khi thoát Strict Mode hoặc khi tắt ứng dụng (`Closing` event).
* **Cơ chế co giãn giao diện tự thích ứng (Responsive Layout)**:
  * Sử dụng `Grid` cố định tỉ lệ cột chuẩn (`3.5*` cho Todo List, `6.5*` cho Đồng hồ). Ảnh nền sử dụng `Stretch="UniformToFill"` để phủ kín 100% màn hình (chấp nhận xén nhẹ mép ảnh) nhằm giữ nguyên vị trí và kích thước gốc của các phần tử UI mà không dùng `Viewbox` bao toàn bộ (tránh hiện tượng đo lường kích thước vô tận Infinite Measurement gây dãn tràn chữ).

### G. Phím tắt Fullscreen & Trải nghiệm Nhanh (F11 & Escape Hotkeys)
* **Phím F11**: Đăng ký sự kiện `PreviewKeyDown` trong `MainWindow.xaml.cs` để chuyển đổi trạng thái Fullscreen (`ApplyStrictFullscreen(!_isStrictFullscreenActive)`).
* **Phím Esc (Escape)**: Khi ứng dụng đang ở chế độ Fullscreen (`_isStrictFullscreenActive`), nhấn phím **Esc** sẽ tự động thoát Fullscreen (`ApplyStrictFullscreen(false)`) mang lại trải nghiệm người dùng mượt mà.

### H. Tối ưu hóa GPU/CPU khi Thu nhỏ (Minimized Performance Optimization)
* **Tự động Pause/Play Video Nền**: Trong sự kiện `StateChanged` của `MainWindow.xaml.cs`, khi `WindowState == WindowState.Minimized`, ứng dụng tạm dừng phát video nền (`BackgroundVideoPlayer.Pause()`) nhằm giảm thiểu việc chiếm dụng tài nguyên GPU/CPU khi ứng dụng chạy ngầm dưới Taskbar. Khi ứng dụng được khôi phục, trình phát tự động tiếp tục (`Play()`).

### I. Khóa Đơn bản Ứng dụng & An toàn Cài đặt (AppMutex & Installer Safety)
* **Mutex Hệ thống**: Khởi tạo `System.Threading.Mutex("DuckPomodoroAppMutex")` trong `App.xaml.cs` để quản lý tiến trình độc nhất của ứng dụng.
* **Tích hợp Inno Setup**: Khai báo `AppMutex=DuckPomodoroAppMutex` trong [installer.iss](file:///d:/Duc/Code/Projects/Pomodoro/installer.iss). Trình cài đặt/gỡ cài đặt sẽ tự động nhắc nhở người dùng đóng ứng dụng đang chạy trước khi ghi đè file `.exe`, loại bỏ hoàn toàn lỗi khóa file trong quá trình nâng cấp phiên bản mới.

---

## 4. Quy trình Đóng gói & Phát hành (Release Workflow)

### Cấu trúc Tệp `.release_credentials.json` (Mẫu)
Tạo tệp `.release_credentials.json` tại thư mục gốc dự án (tệp này đã được `.gitignore` ẩn đi để bảo mật):
```json
{
  "github_token": "ghp_YourPersonalAccessTokenHere",
  "repository": "owner/DuckPomodoro"
}
```

### Các bước phát hành:
1. Chạy thử nghiệm nội bộ bằng bản Debug:
   ```powershell
   dotnet build
   ```
2. Khi hoàn thành sửa lỗi và có yêu cầu **Publish** từ người dùng:
   * Biên dịch bản Release tự chứa (Self-contained) dưới dạng 1 file exe duy nhất:
     ```powershell
     dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
     ```
   * Cập nhật số hiệu phiên bản mới trong tệp cấu hình đóng gói [installer.iss](file:///d:/Duc/Code/Projects/Pomodoro/installer.iss) (Dòng 5: `#define MyAppVersion "X.Y.Z"`).
   * Chạy compiler Inno Setup để tạo trình cài đặt:
     ```powershell
     iscc installer.iss
     ```
   * Đẩy code lên Git, gắn Tag phiên bản mới và tạo Release trên GitHub bằng **GitHub CLI (`gh`)**:
     ```powershell
     # 1. Đọc thông tin credentials và số phiên bản
     $creds = Get-Content .release_credentials.json | ConvertFrom-Json
     $version = "v1.9.1" # Cập nhật tương ứng với installer.iss

     # 2. Commit & Push Tag lên GitHub (Dùng -f nếu cần đè Tag sửa đổi)
     git add .
     git commit -m "Release $version"
     git tag -f $version
     git push origin main $version --force

     # 3. Tạo Release và Upload file DuckPomodoro_Setup.exe lên GitHub
     $env:GH_TOKEN = $creds.github_token
     gh release create $version ".\bin\Release\DuckPomodoro_Setup.exe" --repo $creds.repository --title "Release $version" --notes "Cập nhật phiên bản mới $version" --clobber
     ```

---

## 5. Bảo mật & Quản lý Thông tin nhạy cảm (Security Guidelines)

* **Tệp Credentials (`.release_credentials.json`)**:
  * Tệp chứa `github_token` cá nhân **tuyệt đối không được commit** lên Git công khai.
  * Đã được thêm vào [.gitignore](file:///d:/Duc/Code/Projects/Pomodoro/.gitignore).
  * Chỉ lưu tệp này trên máy cục bộ của Developer hoặc gán thành **Environment Variable / GitHub Actions Secret** khi dựng CI/CD.
* **Đường dẫn tương đối (Relative Paths)**:
  * Sử dụng đường dẫn tương đối (ví dụ `.\bin\Release\...`, `installer.iss`) thay vì đường dẫn tuyệt đối chứa tên thư mục cá nhân người dùng (như `d:\Duc\...` hoặc `C:\Users\ductr\...`) trong tài liệu và kịch bản đóng gói.
* **Mã định danh App / API Keys**:
  * `ClientId` Discord RPC hay AppId Inno Setup được quản lý độc lập. Khi phát hành bản thương mại hoặc chuyển đổi tài khoản Discord Developer, cần cập nhật `ClientId` mới trong `DiscordRpcService.cs` và `AppId` trong `installer.iss`.
