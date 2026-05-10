using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Messages;
using PowerWordRelive.Infrastructure.Security;

namespace PowerWordRelive.LocalBackend.Services;

public class PushConnection
{
    private readonly string _name;
    private readonly string _url;
    private readonly byte[] _key;
    private readonly int _pollIntervalSec;
    private readonly int _maxReconnectAttempts;
    private readonly double _initialReconnectDelaySec;

    public PushConnection(string name, string url, byte[] key, int pollIntervalSec,
        int maxReconnectAttempts, double initialReconnectDelaySec)
    {
        _name = name;
        _url = url;
        _key = key;
        _pollIntervalSec = pollIntervalSec;
        _maxReconnectAttempts = maxReconnectAttempts;
        _initialReconnectDelaySec = initialReconnectDelaySec;
    }

    public async Task RunAsync(DatabaseReader dbReader, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= _maxReconnectAttempts; attempt++)
        {
            if (ct.IsCancellationRequested) return;

            if (attempt > 0)
            {
                var delaySec = _initialReconnectDelaySec * Math.Pow(2, attempt - 1);
                LogRedirector.Info("LocalBackend",
                    $"[{_name}] Reconnect attempt {attempt}/{_maxReconnectAttempts}, waiting {delaySec:F0}s");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            try
            {
                await ConnectAndPushAsync(dbReader, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogRedirector.Warn("LocalBackend", $"[{_name}] Connection failed: {ex.Message}");
            }
        }

        LogRedirector.Error("LocalBackend",
            $"[{_name}] Max reconnect attempts ({_maxReconnectAttempts}) exhausted, exiting");
        Environment.Exit(1);
    }

    private async Task ConnectAndPushAsync(DatabaseReader dbReader, CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(_url), ct);
        LogRedirector.Info("LocalBackend", $"[{_name}] Connected to {_url}");

        if (!await AuthenticateAsync(ws, ct))
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "auth failed", CancellationToken.None);
            throw new InvalidOperationException("Authentication failed");
        }

        LogRedirector.Info("LocalBackend", $"[{_name}] Authenticated");
        await PushLoopAsync(ws, dbReader, ct);
    }

    private async Task<bool> AuthenticateAsync(ClientWebSocket ws, CancellationToken ct)
    {
        using var authCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        authCts.CancelAfter(TimeSpan.FromSeconds(5));

        var challengeMsg = await ReceiveJsonAsync(ws, authCts.Token);
        if (challengeMsg is not { Type: "auth_challenge" } || challengeMsg.Message == null)
            return false;

        var encrypted = AesAuth.EncryptChallenge(challengeMsg.Message, _key);
        var response = new WsMessage { Type = "auth_response", Message = encrypted };
        await SendJsonAsync(ws, response, ct);
        return true;
    }

    private async Task PushLoopAsync(ClientWebSocket ws, DatabaseReader dbReader, CancellationToken ct)
    {
        var pollInterval = TimeSpan.FromSeconds(_pollIntervalSec);
        var lastVersion = -1;

        var receiveTask = Task.Run(async () =>
        {
            try
            {
                var buffer = new byte[8192];
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                }
            }
            catch
            {
            }
        });

        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var currentVersion = dbReader.GetDataVersion();
                if (currentVersion >= 0 && currentVersion != lastVersion)
                {
                    lastVersion = currentVersion;
                    var allData = await dbReader.GetAllDataAsync();
                    var msg = new WsMessage
                    {
                        Type = "data_update",
                        Data = JsonSerializer.SerializeToElement(allData)
                    };
                    await SendJsonAsync(ws, msg, ct);
                    LogRedirector.Debug("LocalBackend", $"[{_name}] Pushed data_update, version={currentVersion}");
                }

                try
                {
                    await Task.Delay(pollInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            try
            {
                await receiveTask;
            }
            catch
            {
            }
        }
    }

    private static async Task SendJsonAsync(ClientWebSocket ws, WsMessage msg, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(msg);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    private static async Task<WsMessage?> ReceiveJsonAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
        if (result.MessageType == WebSocketMessageType.Close)
            return null;

        var ms = new MemoryStream();
        ms.Write(buffer, 0, result.Count);
        while (!result.EndOfMessage)
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            ms.Write(buffer, 0, result.Count);
        }

        var json = Encoding.UTF8.GetString(ms.ToArray());
        return JsonSerializer.Deserialize<WsMessage>(json);
    }
}