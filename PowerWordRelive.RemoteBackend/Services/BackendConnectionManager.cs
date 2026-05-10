using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Messages;
using PowerWordRelive.Infrastructure.Security;

namespace PowerWordRelive.RemoteBackend.Services;

public class BackendConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _frontendClients = new();
    private readonly object _gate = new();
    private readonly byte[] _key;
    private readonly ILogAdapter _log;
    private WebSocket? _backendSocket;
    private string? _lastDataUpdateJson;

    public BackendConnectionManager(byte[] key, ILogAdapter log)
    {
        if (key.Length == 0)
            throw new ArgumentException("Key must not be empty", nameof(key));
        _key = key;
        _log = log;
    }

    public bool IsConnected
    {
        get
        {
            lock (_gate)
            {
                return _backendSocket != null;
            }
        }
    }

    public async Task HandleBackendWebSocket(HttpContext context)
    {
        var ws = await context.WebSockets.AcceptWebSocketAsync();

        try
        {
            var authenticated = await AuthenticateBackend(ws);
            if (!authenticated)
            {
                _log.Warn("Backend auth failed");
                return;
            }

            bool acquired;
            lock (_gate)
            {
                acquired = _backendSocket == null;
                if (acquired) _backendSocket = ws;
            }

            if (!acquired)
            {
                _log.Warn("Rejected authenticated backend: already connected");
                return;
            }

            _log.Info("Backend connected");
            await BroadcastStatusToFrontends(true);
            await RelayLoop(ws);
        }
        finally
        {
            lock (_gate)
            {
                if (_backendSocket == ws)
                    _backendSocket = null;
            }

            _log.Info("Backend disconnected");
            await BroadcastStatusToFrontends(false);
        }
    }

    public async Task HandleFrontendWebSocket(HttpContext context)
    {
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        var clientId = Guid.NewGuid().ToString("N");
        _frontendClients[clientId] = ws;

        _log.Info($"Frontend client {clientId} connected");

        try
        {
            await SendStatusToFrontend(ws);

            var cached = _lastDataUpdateJson;
            if (cached != null)
            {
                var bytes = Encoding.UTF8.GetBytes(cached);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true,
                    CancellationToken.None);
            }

            await FrontendReceiveLoop(ws, clientId);
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            _frontendClients.TryRemove(clientId, out _);
            _log.Info($"Frontend client {clientId} disconnected");
        }
    }

    private async Task<bool> AuthenticateBackend(WebSocket ws)
    {
        var challenge = AesAuth.GenerateChallenge();
        var msg = new WsMessage { Type = "auth_challenge", Message = challenge };
        await SendJson(ws, msg);

        var response = await ReceiveJson(ws, TimeSpan.FromSeconds(5));
        if (response is not { Type: "auth_response" } || response.Message == null)
            return false;

        return AesAuth.VerifyChallenge(challenge, response.Message, _key);
    }

    private async Task RelayLoop(WebSocket backend)
    {
        while (backend.State == WebSocketState.Open)
        {
            var msg = await ReceiveJson(backend, TimeSpan.FromDays(1));
            if (msg == null) break;

            var json = JsonSerializer.Serialize(msg);
            if (msg.Type == "data_update")
                _lastDataUpdateJson = json;

            await BroadcastRaw(json);
        }
    }

    private async Task FrontendReceiveLoop(WebSocket ws, string clientId)
    {
        while (ws.State == WebSocketState.Open)
        {
            var msg = await ReceiveJson(ws, TimeSpan.FromDays(1));
            if (msg == null) break;

            if (msg.Type != "query") continue;

            WebSocket? backend;
            lock (_gate)
            {
                backend = _backendSocket;
            }

            if (backend == null)
            {
                var error = new WsMessage { Type = "error", Id = msg.Id, Message = "backend offline" };
                await SendJson(ws, error);
            }
            else
            {
                await SendJson(backend, msg);
            }
        }
    }

    private async Task BroadcastRaw(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var dead = new List<string>();

        foreach (var (id, ws) in _frontendClients)
        {
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true,
                    CancellationToken.None);
            }
            catch
            {
                dead.Add(id);
            }
        }

        foreach (var id in dead)
            _frontendClients.TryRemove(id, out _);
    }

    private async Task BroadcastStatusToFrontends(bool connected)
    {
        var msg = new WsMessage { Type = "status", BackendConnected = connected };
        var json = JsonSerializer.Serialize(msg);
        await BroadcastRaw(json);
    }

    private async Task SendStatusToFrontend(WebSocket ws)
    {
        var msg = new WsMessage { Type = "status", BackendConnected = IsConnected };
        await SendJson(ws, msg);
    }

    private static async Task SendJson(WebSocket ws, WsMessage msg)
    {
        var json = JsonSerializer.Serialize(msg);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<WsMessage?> ReceiveJson(WebSocket ws, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var buffer = new byte[8192];
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            var ms = new MemoryStream();
            ms.Write(buffer, 0, result.Count);
            while (!result.EndOfMessage)
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                ms.Write(buffer, 0, result.Count);
            }

            var json = Encoding.UTF8.GetString(ms.ToArray());
            return JsonSerializer.Deserialize<WsMessage>(json);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
