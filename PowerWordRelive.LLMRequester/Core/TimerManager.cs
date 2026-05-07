namespace PowerWordRelive.LLMRequester.Core;

public static class TimerManager
{
    public static List<Task> StartTimers(
        Dictionary<string, TimeSpan> intervals,
        ConcurrentRequestQueue queue,
        CancellationToken ct)
    {
        var tasks = new List<Task>();

        foreach (var (key, interval) in intervals)
        {
            var task = Task.Run(() => TimerLoop(key, interval, queue, ct), ct);
            tasks.Add(task);
        }

        return tasks;
    }

    private static async Task TimerLoop(
        string key,
        TimeSpan interval,
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
                queue.Enqueue(key);
        }
    }
}