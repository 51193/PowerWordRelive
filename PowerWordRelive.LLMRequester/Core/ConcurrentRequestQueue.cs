using System.Collections.Concurrent;
using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.LLMRequester.Core;

public class ConcurrentRequestQueue
{
    private static readonly TimeSpan StuckWarnThreshold = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, int> _duplicates = new();

    private readonly ConcurrentQueue<string> _queue = new();
    private readonly ManualResetEventSlim _signal = new(false);
    private readonly object _stuckLock = new();
    private DateTime? _firstEnqueueAt;

    public void Enqueue(string key)
    {
        if (_duplicates.TryGetValue(key, out var count) && count > 0)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Skipping duplicate enqueue for key '{key}', already in queue");
            return;
        }

        var wasEmpty = _queue.IsEmpty;
        _queue.Enqueue(key);

        _duplicates.AddOrUpdate(key, 1, (_, c) => c + 1);

        lock (_stuckLock)
        {
            _firstEnqueueAt ??= DateTime.UtcNow;
        }

        if (wasEmpty)
            _signal.Set();
    }

    public bool TryPeek(out string? key)
    {
        return _queue.TryPeek(out key);
    }

    public void Complete(string expectedKey)
    {
        if (_queue.TryDequeue(out var key)) _duplicates.AddOrUpdate(key, 0, (_, c) => Math.Max(0, c - 1));

        if (_queue.IsEmpty)
        {
            _signal.Reset();
            lock (_stuckLock)
            {
                _firstEnqueueAt = null;
            }
        }
    }

    public void Wait(CancellationToken ct)
    {
        _signal.Wait(ct);
    }

    public void CheckStuck()
    {
        DateTime? first;
        lock (_stuckLock)
        {
            first = _firstEnqueueAt;
        }

        if (first != null && DateTime.UtcNow - first.Value > StuckWarnThreshold)
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Queue has been non-empty since {first.Value:O}, " +
                $"items may be stuck. Approximate size: {_queue.Count}");
    }
}