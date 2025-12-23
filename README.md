## 👨‍💻 Tác giả

Võ Minh Khang-24120336
Vũ Đức Trung-24120479
Nguyễn Hồng Quang-24120220
# 🖥️ Remote Control - Ứng dụng điều khiển máy tính từ xa

## 📖 Giới thiệu

Ứng dụng điều khiển máy tính từ xa qua giao diện web hiện đại, hỗ trợ điều khiển qua Internet thông qua ngrok.

## ✨ Chức năng

| #   | Chức năng               | Mô tả                                                     |
| --- | ----------------------- | --------------------------------------------------------- |
| 1   | 📱 **Quản lý ứng dụng** | Xem, khởi động, dừng các ứng dụng đang chạy               |
| 2   | ⚙️ **Task Manager**     | Xem tất cả processes, tìm kiếm, kill process              |
| 3   | ⚡ **Quick Launch**     | Mở nhanh các ứng dụng phổ biến (Notepad, Chrome, Word...) |
| 4   | 🔌 **Shutdown**         | Tắt máy tính từ xa                                        |
| 5   | 🔄 **Restart**          | Khởi động lại máy tính từ xa                              |
| 6   | 📷 **Webcam Control**   | Tắt/Bật webcam trên máy bị điều khiển                     |
| 7   | 🖼️ **Screenshot**       | Chụp màn hình từ xa, tải xuống ảnh                        |
| 8   | ⌨️ **Keylogger**        | Ghi lại các phím được nhấn (có auto-refresh)              |

## 📁 Cấu trúc Project

```
RemoteControl/
├── WebServer/                    # Server điều khiển
│   ├── Program.cs               # API & WebSocket server
│   └── wwwroot/
│       ├── index.html           # Giao diện HTML
│       ├── css/
│       │   └── style.css        # Styles
│       └── js/
│           └── app.js           # JavaScript logic
│
├── RemoteAgent/                  # Agent chạy trên máy bị điều khiển
│   └── Program.cs               # WebSocket client
│
└── RemoteControl.sln            # Solution file
```


## 💻 Yêu cầu hệ thống

### 1. Máy Điều Khiển (Hacker/Admin)
- Cài đặt **.NET 6.0 SDK** trở lên (để build code).
- Cài đặt **ngrok** (để public server ra Internet).

### 2. Máy Bị Điều Khiển (Victim)
- **Hệ điều hành:** Windows 10/11 (64-bit).
- **Môi trường:** **KHÔNG YÊU CẦU** (Không cần cài .NET vì đã tích hợp sẵn).
- **Mạng:** Có kết nối Internet.

---

## 🚀 QUY TRÌNH SỬ DỤNG (3 BƯỚC)

### 📍 Bước 1: Khởi động Server (Trên máy bạn)

1. **Chạy WebServer:**
   Mở Terminal tại thư mục `WebServer` và chạy:
   ```powershell
   cd D:\hoc\MMT\RemoteControl\WebServer
   dotnet run
✅ WebServer chạy tại: http://localhost:5000

2. **Mở Ngrok: Mở một Terminal mới và chạy:**
    PowerShell:

        ngrok http 5000

📋 **Copy đường dẫn Forwarding (Ví dụ: https://abc123xyz.ngrok-free.app).**

### 📍 Bước 2: Tạo file Agent "Độc lập" (Trên máy bạn)
    **Đây là bước đóng gói code thành 1 file .exe duy nhất để gửi đi.**

    1.Mở Terminal tại thư mục RemoteAgent.

    2.Chạy lệnh Build:

    PowerShell:

        dotnet publish -c Release -r win-x64 --self-contained

    3.Lấy hàng: Truy cập vào thư mục sau để lấy file: RemoteAgent\bin\Release\net6.0\win-x64\publish\ 
    👉 Bạn sẽ thấy file RemoteAgent.exe (Dung lượng khoảng ~60MB).
### 📍 Bước 3: Tấn công (Trên máy nạn nhân)

   1. **Gửi file**: Copy file RemoteAgent.exe (vừa lấy ở Bước 2) sang máy nạn nhân (qua USB, Drive, Zalo...).
   
   2. **Copy đường dẫn thư mục:** Vào thư mục chứa file `RemoteAgent.exe`, bấm vào thanh địa chỉ ở trên cùng và copy đường dẫn.
   
   3. **Mở PowerShell Admin:**
       - Nhấn phím **Windows**, gõ chữ `powershell`.
       - Chọn **"Run as Administrator"** bên tay phải.
   
   4. **Di chuyển vào thư mục:**
       - Gõ lệnh: `cd "dán-đường-dẫn-vừa-copy-vào-đây"` rồi Enter.
   
   5. **Chạy lệnh kết nối:**
       ```powershell
       .\RemoteAgent.exe <link-ngrok-của-bạn>

### Bước 6: Mở giao diện điều khiển

1. Mở trình duyệt: **http://localhost:5000**
2. Chọn máy tính từ danh sách bên trái
3. Sử dụng các tab để điều khiển

## 📋 Chi tiết các Tab

### 📱 Tab Ứng dụng

- Xem danh sách ứng dụng đang chạy (có cửa sổ)
- Thông tin: Tên, Tiêu đề, PID, Threads, RAM
- Dừng ứng dụng
- Khởi động ứng dụng mới

### ⚙️ Tab Processes

- Xem tất cả processes (như Task Manager)
- Tìm kiếm process theo tên
- Kill process theo PID
- Quick launch: Mở nhanh Notepad, Calc, CMD, Chrome, Edge...

### ⚡ Tab Nguồn

- **Shutdown**: Tắt máy sau 10 giây
- **Restart**: Khởi động lại sau 10 giây

### ⌨️ Tab Keylogger

- Bắt đầu/Dừng ghi phím
- Auto-refresh mỗi 2 giây
- Hiển thị phím đặc biệt: [ENTER], [BACKSPACE], [SHIFT]...

### 📷 Tab Webcam

- Tắt webcam (Disable device)
- Bật webcam (Enable device)
- Yêu cầu quyền Administrator

### 🖼️ Tab Chụp màn hình

- Chụp màn hình từ xa
- Xem ảnh fullscreen
- Tải xuống ảnh (JPEG)

## 🔧 Giao thức truyền thông

- **Protocol**: WebSocket (wss://)
- **Port**: 5000 (HTTP) → ngrok tunnel
- **Format lệnh**: `COMMAND|param1|param2`
- **Format phản hồi**: `TYPE|data`

### Danh sách lệnh:

| Lệnh                  | Mô tả                   |
| --------------------- | ----------------------- |
| `LIST_APPS`           | Lấy danh sách ứng dụng  |
| `LIST_PROCESSES`      | Lấy danh sách processes |
| `START_PROCESS\|name` | Khởi động ứng dụng      |
| `KILL_PROCESS\|pid`   | Dừng process            |
| `SHUTDOWN`            | Tắt máy                 |
| `RESTART`             | Khởi động lại           |
| `DISABLE_WEBCAM`      | Tắt webcam              |
| `ENABLE_WEBCAM`       | Bật webcam              |
| `SCREENSHOT`          | Chụp màn hình           |
| `START_KEYLOGGER`     | Bắt đầu keylogger       |
| `STOP_KEYLOGGER`      | Dừng keylogger          |
| `GET_KEYLOG`          | Lấy nội dung keylog     |

## ⚠️ Lưu ý

- Agent tự động kết nối lại khi mất kết nối
- Shutdown/Restart có delay 10 giây để hủy nếu cần (`shutdown /a`)
- Webcam control cần quyền Administrator
- Keylogger chỉ hoạt động khi có message loop (Windows)

## 🛡️ Cảnh báo pháp lý

⚠️ **Chỉ sử dụng cho mục đích học tập và trên máy tính của chính bạn hoặc có sự đồng ý của chủ sở hữu.**

Việc truy cập trái phép vào máy tính của người khác là vi phạm pháp luật.

