using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Messages;

namespace PowerWordRelive.RemoteBackend.Services;

public class BackendConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _frontendClients = new();
    private readonly object _gate = new();
    private readonly byte[] _key;
    private readonly ILogAdapter _log;
    private WebSocket? _backendSocket;

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
        var buffer = new byte[8192];

        try
        {
            var authenticated = await AuthenticateBackend(ws, buffer);
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
            await RelayLoop(ws, buffer);
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
            var buffer = new byte[8192];
            await FrontendReceiveLoop(ws, buffer, clientId);
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

    private async Task<bool> AuthenticateBackend(WebSocket ws, byte[] buffer)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var challenge = new WsMessage { Type = "auth_challenge", Message = nonce };
        await SendJson(ws, challenge);

        var result = await ReceiveJson(ws, buffer, TimeSpan.FromSeconds(5));
        if (result is not { Type: "auth_response" } || result.Message == null)
            return false;

        return VerifyAuth(nonce, result.Message);
    }

    private bool VerifyAuth(string nonce, string encryptedResponse)
    {
        try
        {
            var data = Convert.FromBase64String(encryptedResponse);
            var iv = data[..16];
            var ciphertext = data[16..];

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            var decrypted = Encoding.UTF8.GetString(plainBytes);
            return decrypted == nonce;
        }
        catch
        {
            return false;
        }
    }

    private async Task FrontendReceiveLoop(WebSocket ws, byte[] buffer, string clientId)
    {
        while (ws.State == WebSocketState.Open)
        {
            var msg = await ReceiveJson(ws, buffer, TimeSpan.FromDays(1));
            if (msg == null) break;

            WebSocket? backend;
            lock (_gate)
            {
                backend = _backendSocket;
            }

            if (backend == null)
            {
                var error = new WsMessage
                {
                    Type = "error",
                    Id = msg.Id,
                    Message = "backend offline"
                };
                await SendJson(ws, error);
                continue;
            }

            if (msg.Type == "query")
                await SendJson(backend, msg);
        }
    }

    private async Task RelayLoop(WebSocket backend, byte[] buffer)
    {
        while (backend.State == WebSocketState.Open)
        {
            var msg = await ReceiveJson(backend, buffer, TimeSpan.FromDays(1));
            if (msg == null) break;

            await BroadcastToFrontends(msg);
        }
    }

    private async Task BroadcastToFrontends(WsMessage msg)
    {
        var dead = new List<string>();
        foreach (var (id, ws) in _frontendClients)
            try
            {
                await SendJson(ws, msg);
            }
            catch
            {
                dead.Add(id);
            }

        foreach (var id in dead)
            _frontendClients.TryRemove(id, out _);
    }

    private async Task BroadcastStatusToFrontends(bool connected)
    {
        var msg = new WsMessage { Type = "status", BackendConnected = connected };
        await BroadcastToFrontends(msg);
    }

    private async Task SendStatusToFrontend(WebSocket ws)
    {
        var connected = IsConnected;
        var msg = new WsMessage { Type = "status", BackendConnected = connected };
        await SendJson(ws, msg);
    }

    private static async Task SendJson(WebSocket ws, WsMessage msg)
    {
        var json = JsonSerializer.Serialize(msg);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<WsMessage?> ReceiveJson(WebSocket ws, byte[] buffer, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
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
