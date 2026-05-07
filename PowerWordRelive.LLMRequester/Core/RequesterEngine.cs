using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.LLMRequester.Core;

public class RequesterEngine
{
    private readonly ConcurrentRequestQueue _queue;
    private readonly Dictionary<string, IRequest> _registry;

    public RequesterEngine(ConcurrentRequestQueue queue, Dictionary<string, IRequest> registry)
    {
        _queue = queue;
        _registry = registry;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _queue.Wait(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (_queue.TryPeek(out var key) && key != null && !ct.IsCancellationRequested)
            {
                if (_registry.TryGetValue(key, out var request))
                    try
                    {
                        await request.Request();
                    }
                    catch (Exception ex)
                    {
                        LogRedirector.Error("PowerWordRelive.LLMRequester",
                            $"Request '{key}' failed: {ex.Message}");
                    }
                else
                    LogRedirector.Error("PowerWordRelive.LLMRequester",
                        $"Unknown request key in queue: {key}");

                _queue.Complete(key);
                _queue.CheckStuck();
            }
        }
    }
}