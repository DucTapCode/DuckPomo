# Duck Pomodoro - Hướng dẫn Phát triển & Sửa lỗi (Developer Guide)

Tài liệu này tổng hợp toàn bộ kiến thức kiến trúc, thiết kế, và các giải pháp sửa lỗi đặc thù trong dự án **Duck Pomodoro** để hỗ trợ nhà phát triển (hoặc AI trợ lý) dễ dàng tiếp cận, bảo trì và sửa lỗi trong tương lai.

---

## 1. Kiến trúc Dự án (Project Architecture)

Dự án được xây dựng trên nền tảng **WPF (.NET 10.0-windows)** theo mô hình chuẩn **MVVM (Model-View-ViewModel)**:
*   **Views (`MainWindow.xaml` / `MainWindow.xaml.cs`)**: Quản lý giao diện, vòng đời cửa sổ, các sự kiện chuột/bàn phím và các cấu hình đồ họa Win32 đặc thù (Window Chrome, Hooks).
*   **ViewModels (`ViewModels\MainViewModel.cs`)**: Chứa logic nghiệp vụ của đồng hồ Pomodoro, quản lý danh sách công việc (Todo List), trạng thái cấu hình hệ thống, và liên kết dữ liệu (Data Binding).
*   **Services (`Services\`)**:
    *   `DataService.cs`: Đọc/ghi cấu hình người dùng và danh sách công việc xuống file JSON.
    *   `AudioService.cs`: Tạo âm thanh tích tắc nhịp nhàng bằng thư viện âm thanh tần số thấp.
    *   `DiscordRpcService.cs`: Đồng bộ hóa trạng thái học tập/nghỉ ngơi lên Discord Rich Presence.
*   **Installer Config (`installer.iss`)**: Kịch bản đóng gói ứng dụng thành file cài đặt Windows thông qua **Inno Setup 6**.

---

## 2. Các giải pháp kỹ thuật đặc thù & Sửa lỗi (Critical Bug Fixes)

### A. Giao diện Kính mờ (Glassmorphism / Acrylic Custom Window)
*   **Cơ chế**: Cửa sổ chính được thiết kế không viền hệ thống (`WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`).
*   **Pha màu kính thích ứng (Adaptive Acrylic Tinting)**:
    *   Khi người dùng tải hình nền (Custom Image), thuật toán sẽ trích xuất màu sắc chủ đạo dựa trên công thức **Độ sáng tương đối (Relative Luminance)** của W3C:
        $$Y = \frac{0.2126 \times R + 0.7152 \times G + 0.0722 \times B}{255}$$
    *   Nếu $Y < 0.45$, hình nền được nhận diện là **Tối (Dark)** $\rightarrow$ Đổi chữ sang màu trắng và pha kính tối. Ngược lại là **Sáng (Light)** $\rightarrow$ Đổi chữ sang màu đen và pha kính sáng.
    *   Để tạo hiệu ứng hòa hợp màu sắc (Acrylic), ứng dụng sẽ **pha thêm 20% màu chủ đạo** của hình nền vào lớp kính mờ có **độ trong suốt 15% (Opacity = 40/255)**.
*   **Sửa lỗi phóng to (Maximize) không lấp đầy và bị hở viền**:
    *   **Giải pháp**: Nhờ có Win32 Hook `WM_GETMINMAXINFO` quản lý chính xác phạm vi làm việc của màn hình (`rcWorkArea`), chúng ta không cần co lề `8px` nữa (giữ `Margin = 0` cố định). Chỉ cần gọi `this.UpdateLayout()` trong sự kiện `StateChanged` để ép buộc WPF cập nhật toàn bộ khung bố trí và lấp đầy màn hình khi phóng to.

### B. Trình phát GIF động Native (Custom GIF Renderer)
*   **Vấn đề**: `MediaElement` mặc định của WPF giải mã GIF rất kém, thường xuyên bị giật, nháy đen khung hình hoặc rò rỉ pixel trong suốt làm lộ IDE/Desktop phía sau.
*   **Giải pháp**:
    *   Sử dụng lớp **`GifBitmapDecoder`** để tách toàn bộ khung hình tĩnh của GIF và lấy thời gian trễ của từng khung.
    *   Dùng **`RenderTargetBitmap`** kết hợp **`DrawingVisual`** tạo một canvas đệm tích lũy. Mỗi khung hình mới sẽ được vẽ đè lên các khung hình trước để giữ nguyên cấu trúc pixel trong suốt (Disposal Method: Combine).
    *   Phát GIF thông qua đối tượng `Image` thông thường, loại bỏ hoàn toàn `MediaElement` đối với định dạng `.gif`.

### C. Cơ chế Maximize của Cửa sổ không viền (Win32 Interop Hook)
*   Để ngăn cửa sổ không viền tràn đè lên thanh Taskbar khi phóng to, ứng dụng cài đặt một Win32 Hook lắng nghe thông điệp `WM_GETMINMAXINFO` (0x0024):
    ```csharp
    mmi.ptMaxPosition.X = rcWorkArea.Left;
    mmi.ptMaxPosition.Y = rcWorkArea.Top;
    mmi.ptMaxSize.X = rcWorkArea.Right - rcWorkArea.Left;
    mmi.ptMaxSize.Y = rcWorkArea.Bottom - rcWorkArea.Top;
    ```
    *Lưu ý*: Phải sử dụng tọa độ làm việc gốc `rcWorkArea` thay vì tính `Math.Abs` để hỗ trợ hiển thị hoàn hảo trên các thiết lập đa màn hình (Multi-Monitor) có tọa độ làm việc âm.

### D. Discord Rich Presence (Discord RPC)
*   Đồng bộ trạng thái thông qua `DiscordRpcClient`.
*   *Lưu ý quan trọng*: Khi thay đổi `ClientId` mới, người dùng bắt buộc phải truy cập Discord Developer Portal và tải lên một tệp tin hình ảnh làm Icon đại diện của ứng dụng trong phần **Rich Presence -> Art Assets** và đặt tên chính xác là **`icon`** (viết thường). Nếu không có asset này, Discord sẽ chặn không hiển thị RPC.

---

## 3. Quy trình Đóng gói & Phát hành (Release Workflow)

Khi cần biên dịch và xuất bản phiên bản mới:
1.  Chạy thử nghiệm nội bộ bằng bản Debug:
    ```powershell
    dotnet build
    ```
2.  Khi hoàn thành sửa lỗi và có yêu cầu **Publish** từ người dùng:
    *   Biên dịch bản Release tự chứa (Self-contained) dưới dạng 1 file exe duy nhất:
        ```powershell
        dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
        ```
    *   Cập nhật số hiệu phiên bản mới trong tệp cấu hình đóng gói [installer.iss](file:///d:/Duc/Code/Projects/Pomodoro/installer.iss) (Dòng 5: `#define MyAppVersion "X.Y.Z"`).
    *   Chạy compiler Inno Setup để tạo trình cài đặt:
        ```powershell
        $env:Path += ";C:\Users\ductr\AppData\Local\Programs\Inno Setup 6"; iscc installer.iss
        ```
    *   Đẩy code lên Git và tạo Tag/Release mới trên GitHub tương ứng với số hiệu phiên bản.
