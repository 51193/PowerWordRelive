using PowerWordRelive.Infrastructure.Storage;
using PowerWordRelive.RemoteBackend.Services;

var fs = new LocalFileSystem();
var config = new ConfigService(fs);

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls($"http://0.0.0.0:{config.Port}");

builder.Services.AddSingleton(fs);
builder.Services.AddSingleton(sp => new BackendConnectionManager(
    config.KeyPath, fs, sp.GetRequiredService<ILogger<BackendConnectionManager>>()));

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.UseDefaultFiles();
app.UseStaticFiles();

app.Map("/ws/backend", async (HttpContext context, BackendConnectionManager manager) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    await manager.HandleBackendWebSocket(context);
});

app.Map("/ws/frontend", async (HttpContext context, BackendConnectionManager manager) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    await manager.HandleFrontendWebSocket(context);
});

app.Run();