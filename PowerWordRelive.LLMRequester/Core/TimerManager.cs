namespace PowerWordRelive.LLMRequester.Core;

public static class TimerManager
{
    public static List<Task> StartTimers(
        Dictionary<TimeSpan, List<string>> intervals,
        ConcurrentRequestQueue queue,
        CancellationToken ct)
    {
        var tasks = new List<Task>();

        foreach (var (interval, keys) in intervals)
        {
            var task = Task.Run(() => TimerLoop(interval, keys, queue, ct), ct);
            tasks.Add(task);
        }

        return tasks;
    }

    private static async Task TimerLoop(
        TimeSpan interval,
        List<string> keys,
        ConcurrentRequestQueue queue,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!ct.IsCancellationRequested)
                foreach (var key in keys)
                    queue.Enqueue(key);
        }
    }
}
