using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Messages;
using PowerWordRelive.LocalBackend.Models;

namespace PowerWordRelive.LocalBackend.Services;

public class RemoteConnectionService
{
    private readonly byte[] _key;
    private readonly LocalBackendOptions _options;

    public RemoteConnectionService(LocalBackendOptions options)
    {
        _options = options;
        _key = options.Key;
    }

    public async Task ConnectAndRelayAsync(DatabaseReadService dbService)
    {
        var url = $"ws://{_options.Host}:{_options.Port}/ws/backend";
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(url), CancellationToken.None);
        LogRedirector.Info("LocalBackend", $"Connected to {url}");

        var authenticated = await Authenticate(ws);
        if (!authenticated)
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "auth failed", CancellationToken.None);
            throw new Exception("Authentication failed");
        }

        LogRedirector.Info("LocalBackend", "Authenticated");
        var buffer = new byte[8192];
        await RelayLoop(ws, buffer, dbService);
    }

    private async Task<bool> Authenticate(ClientWebSocket ws)
    {
        var buffer = new byte[8192];
        var challengeMsg = await ReceiveJson(ws, buffer, TimeSpan.FromSeconds(5));
        if (challengeMsg is not { Type: "auth_challenge" } || challengeMsg.Message == null)
            return false;

        var encrypted = EncryptAes(challengeMsg.Message);
        var response = new WsMessage { Type = "auth_response", Message = encrypted };
        await SendJson(ws, response);

        return true;
    }

    private string EncryptAes(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    private async Task RelayLoop(ClientWebSocket ws, byte[] buffer, DatabaseReadService dbService)
    {
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var msg = await ReceiveJson(ws, buffer, TimeSpan.FromDays(1));
                if (msg == null)
                {
                    LogRedirector.Warn("LocalBackend",
                        $"WebSocket closed, state={ws.State}, closeStatus={ws.CloseStatus}");
                    break;
                }

                if (msg.Type != "query" || msg.Query == null)
                    continue;

                try
                {
                    var (data, total) = await ExecuteQuery(dbService, msg.Query, msg.Params);
                    var response = new WsMessage
                    {
                        Type = "result",
                        Id = msg.Id,
                        Data = JsonSerializer.SerializeToElement(data),
                        Total = total
                    };
                    await SendJson(ws, response);
                }
                catch (Exception ex)
                {
                    LogRedirector.Warn("LocalBackend", $"Query '{msg.Query}' failed: {ex.Message}");
                    var error = new WsMessage
                    {
                        Type = "error",
                        Id = msg.Id,
                        Message = ex.Message
                    };
                    await SendJson(ws, error);
                }
            }
        }
        catch (Exception ex)
        {
            LogRedirector.Warn("LocalBackend",
                $"RelayLoop exception, ws.State={ws.State}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<(object data, int total)> ExecuteQuery(
        DatabaseReadService dbService, string query, JsonElement? rawParams)
    {
        var dict = new Dictionary<string, string>();
        if (rawParams != null)
            foreach (var prop in rawParams.Value.EnumerateObject())
                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var v))
                    dict[prop.Name] = v.ToString();
                else if (prop.Value.ValueKind == JsonValueKind.String)
                    dict[prop.Name] = prop.Value.GetString() ?? "";

        var limit = int.TryParse(dict.GetValueOrDefault("limit", "50"), out var l) ? l : 50;
        var offset = int.TryParse(dict.GetValueOrDefault("offset", "0"), out var o) ? o : 0;

        return query switch
        {
            "list_refinements" => await dbService.ListRefinementsAsync(limit, offset),
            "list_transcriptions" => await dbService.ListTranscriptionsAsync(limit, offset),
            "list_story_progress" => await dbService.ListStoryProgressAsync(limit, offset),
            "list_tasks" => await dbService.ListTasksAsync(
                dict.GetValueOrDefault("status", "in_progress"), limit, offset),
            "list_consistency" => await dbService.ListConsistencyAsync(limit, offset),
            _ => throw new Exception($"Unknown query: {query}")
        };
    }

    private static async Task SendJson(ClientWebSocket ws, WsMessage msg)
    {
        var json = JsonSerializer.Serialize(msg);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<WsMessage?> ReceiveJson(ClientWebSocket ws, byte[] buffer, TimeSpan timeout)
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
