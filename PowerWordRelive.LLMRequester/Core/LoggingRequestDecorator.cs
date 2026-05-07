using System.Diagnostics;
using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.LLMRequester.Core;

public class LoggingRequestDecorator : IRequest
{
    private readonly string _key;
    private readonly IRequest _inner;

    public LoggingRequestDecorator(string key, IRequest inner)
    {
        _key = key;
        _inner = inner;
    }

    public async Task Request()
    {
        LogRedirector.Info("PowerWordRelive.LLMRequester", $"Dispatching request: {_key}");

        var sw = Stopwatch.StartNew();
        await _inner.Request();
        sw.Stop();

        LogRedirector.Info("PowerWordRelive.LLMRequester", $"Request completed: {_key}",
            new { elapsedMs = sw.ElapsedMilliseconds });
    }
}