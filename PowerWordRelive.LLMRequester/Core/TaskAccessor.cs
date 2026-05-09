using System.Text;
using Microsoft.Data.Sqlite;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.LLMRequester.Database;

namespace PowerWordRelive.LLMRequester.Core;

public class TaskAccessor
{
    private const string EmptyStateMarker = "__EMPTY__";
    private readonly int _activeTaskLimit;

    private readonly LLMDatabase _db;

    public TaskAccessor(LLMDatabase db, int activeTaskLimit)
    {
        _db = db;
        _activeTaskLimit = activeTaskLimit;
    }

    public string BuildActiveTasksText()
    {
        try
        {
            var totalCount = _db.CountActiveTasks();
            if (totalCount == 0)
                return EmptyStateMarker;

            if (totalCount > _activeTaskLimit)
                LogRedirector.Error("PowerWordRelive.LLMRequester",
                    $"Active task count ({totalCount}) exceeds window limit ({_activeTaskLimit}), only showing first {_activeTaskLimit} tasks");

            var entries = _db.GetActiveTasks(_activeTaskLimit);

            var sb = new StringBuilder();
            foreach (var (_, summary, detail) in entries)
                sb.AppendLine($"{summary}：{detail}");

            return sb.ToString().TrimEnd();
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Active tasks query failed (DB not ready): {ex.Message}");
            return EmptyStateMarker;
        }
    }

    public string BuildFinishedTasksText(int limit)
    {
        try
        {
            var entries = _db.GetRecentFinishedTasks(limit);
            if (entries.Count == 0)
                return "(暂无已完成任务)";

            var sb = new StringBuilder();
            foreach (var (_, summary, detail, status) in entries)
            {
                var statusLabel = status switch
                {
                    "complete" => "已完成",
                    "fail" => "已失败",
                    "discard" => "已放弃",
                    _ => status
                };
                sb.AppendLine($"{summary} [{statusLabel}]：{detail}");
            }

            return sb.ToString().TrimEnd();
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Finished tasks query failed (DB not ready): {ex.Message}");
            return "(暂无已完成任务)";
        }
    }
}