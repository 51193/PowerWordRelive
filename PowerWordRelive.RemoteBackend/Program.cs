using PowerWordRelive.RemoteBackend.Services;

var cfg = StartupConfig.Create(args);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = cfg.ContentRoot,
    Args = cfg.BuilderArgs
});
cfg.RegisterServices(builder.Services, cfg.Key);

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
