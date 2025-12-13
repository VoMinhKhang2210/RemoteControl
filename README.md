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

- .NET 6.0 SDK trở lên
- Windows OS
- ngrok (để điều khiển qua Internet)

## 🚀 Hướng dẫn sử dụng

### 📍 Đường dẫn project: `D:\hoc\MMT\RemoteControl`

---

## 🖥️ TRÊN MÁY ĐIỀU KHIỂN (Máy của bạn)

### Bước 1: Mở Terminal và Build project

```powershell
cd D:\hoc\MMT\RemoteControl
dotnet restore
dotnet build
```

### Bước 2: Chạy WebServer

```powershell
dotnet run --project D:\hoc\MMT\RemoteControl\WebServer\WebServer.csproj
```

Hoặc:

```powershell
cd D:\hoc\MMT\RemoteControl\WebServer
dotnet run
```

✅ WebServer chạy tại: **http://localhost:5000**

### Bước 3: Chạy ngrok (để điều khiển qua Internet)

Mở terminal mới:

```powershell
ngrok http 5000
```

📋 Copy URL ngrok, ví dụ: `abc123xyz.ngrok-free.app`

---

## 💻 TRÊN MÁY BỊ ĐIỀU KHIỂN (Máy khác)

### Bước 4: Gửi folder RemoteAgent

Gửi **toàn bộ folder `RemoteAgent`** cho máy cần điều khiển:

- Copy folder: `D:\hoc\MMT\RemoteControl\RemoteAgent`
- Gửi qua USB, Zalo, Google Drive, v.v.

### Bước 5: Chạy RemoteAgent trên máy bị điều khiển

**Cách 1: Chạy bằng dotnet (cần cài .NET SDK)**

```powershell
cd RemoteAgent
dotnet run <ngrok-url>
```

Ví dụ:

```powershell
dotnet run abc123xyz.ngrok-free.app
```

**Cách 2: Build thành file .exe rồi gửi (không cần cài .NET)**

Trên máy bạn, chạy:

```powershell
cd D:\hoc\MMT\RemoteControl\RemoteAgent
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
```

Gửi folder `RemoteAgent/publish/` cho máy bị điều khiển, chạy:

```powershell
RemoteAgent.exe abc123xyz.ngrok-free.app
```

---

## 🎮 ĐIỀU KHIỂN

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

## 👨‍💻 Tác giả

Đồ án học tập - Lập trình mạng với Socket
