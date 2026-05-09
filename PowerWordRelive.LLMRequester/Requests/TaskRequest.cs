using System.Text;
using Microsoft.Data.Sqlite;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Prompt;
using PowerWordRelive.LLMRequester.Core;
using PowerWordRelive.LLMRequester.Database;
using PowerWordRelive.LLMRequester.Parsing;

namespace PowerWordRelive.LLMRequester.Requests;

internal class TaskRequest : IRequest
{
    private const string SystemPromptFile = "prompts/task/task_system.md";
    private const string UserPromptFile = "prompts/task/task_user.md";
    private const string EmptyStateMarker = "__EMPTY__";

    private static readonly string[] ValidFinishStatuses = { "complete", "fail", "discard" };

    private readonly string _apiUrl;
    private readonly string _token;
    private readonly LLMDatabase _db;
    private readonly RefinementContainer _refContainer;
    private readonly StoryProgressContainer _spContainer;
    private readonly PromptAssembler _assembler;
    private readonly TaskConfig _config;
    private readonly LlmApiClient _apiClient;
    private readonly TaskParser _parser = new();

    public TaskRequest(
        string apiUrl,
        string token,
        LLMDatabase db,
        RefinementContainer refContainer,
        StoryProgressContainer spContainer,
        PromptAssembler assembler,
        TaskConfig config,
        LlmApiClient apiClient)
    {
        _apiUrl = apiUrl;
        _token = token;
        _db = db;
        _refContainer = refContainer;
        _spContainer = spContainer;
        _assembler = assembler;
        _config = config;
        _apiClient = apiClient;
    }

    public async Task Request()
    {
        if (!_db.TryEnsureTaskTable() || !_db.TryEnsureTaskFinishLogTable())
            return;

        var refinementText = BuildRefinementWindow(_config.RefinementWindow);
        var storyProgressText = BuildStoryProgressWindow(_config.StoryProgressWindow);
        var activeTasksText = BuildActiveTasksWindow(_config.ActiveTaskWindow);
        var finishedTasksText = BuildFinishedTasksWindow(_config.FinishedTaskWindow);

        var emptyVars = new Dictionary<string, string>();
        var userVars = new Dictionary<string, string>
        {
            ["refinement_window"] = refinementText,
            ["story_progress_window"] = storyProgressText,
            ["active_tasks"] = activeTasksText,
            ["finished_tasks"] = finishedTasksText
        };

        string systemPrompt;
        string userPrompt;

        try
        {
            systemPrompt = _assembler.Assemble(SystemPromptFile, emptyVars);
            userPrompt = _assembler.Assemble(UserPromptFile, userVars);
        }
        catch (Exception ex)
        {
            LogRedirector.Error("PowerWordRelive.LLMRequester",
                $"Task prompt assembly failed: {ex.Message}");
            return;
        }

        LlmResponse response;
        try
        {
            response = await _apiClient.SendAsync(_apiUrl, _token, _config,
                systemPrompt, userPrompt);
        }
        catch (Exception ex)
        {
            LogRedirector.Error("PowerWordRelive.LLMRequester",
                $"Task API call failed: {ex.Message}");
            return;
        }

        var content = response.Content.Trim();
        if (string.IsNullOrEmpty(content) || content == "EMPTY")
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                "Task returned EMPTY, no changes needed");
            return;
        }

        var operations = _parser.Parse(content);
        if (operations.Count == 0)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                "No valid task operations parsed");
            return;
        }

        LogRedirector.Info("PowerWordRelive.LLMRequester",
            $"Applying {operations.Count} task operation(s)");

        foreach (var op in operations)
            try
            {
                ApplyOperation(op);
            }
            catch (Exception ex)
            {
                LogRedirector.Error("PowerWordRelive.LLMRequester",
                    $"Failed to apply task operation {op.Type}: {ex.Message}");
            }
    }

    private void ApplyOperation(TaskOperation op)
    {
        switch (op.Type)
        {
            case TaskOperation.OperationType.Append:
            {
                var existingId = _db.FindActiveTaskIdByKey(op.Key!);
                if (existingId != null)
                {
                    LogRedirector.Warn("PowerWordRelive.LLMRequester",
                        $"Task append skipped: key '{op.Key}' already exists among active tasks");
                    return;
                }

                _db.InsertTask(op.Key!, op.Value!);
                break;
            }

            case TaskOperation.OperationType.Remove:
            {
                var id = ResolveActiveTaskId(op.Key!);
                if (id == null)
                    return;
                _db.DeleteTask(id.Value);
                break;
            }

            case TaskOperation.OperationType.Edit:
            {
                var id = ResolveActiveTaskId(op.Key!);
                if (id == null)
                    return;
                _db.UpdateTaskDetail(id.Value, op.Key!, op.Value!);
                break;
            }

            case TaskOperation.OperationType.Replace:
            {
                var id = ResolveActiveTaskId(op.Key!);
                if (id == null)
                    return;

                if (op.Key != op.NewKey)
                {
                    var conflictId = _db.FindActiveTaskIdByKey(op.NewKey!);
                    if (conflictId != null && conflictId != id)
                    {
                        LogRedirector.Warn("PowerWordRelive.LLMRequester",
                            $"Task replace skipped: new key '{op.NewKey}' conflicts with existing active task");
                        return;
                    }
                }

                _db.UpdateTaskDetail(id.Value, op.NewKey!, op.Value!);
                break;
            }

            case TaskOperation.OperationType.Finish:
            {
                var id = ResolveActiveTaskId(op.Key!);
                if (id == null)
                    return;
                _db.SetTaskStatus(id.Value, op.Status!);
                break;
            }
        }
    }

    private int? ResolveActiveTaskId(string key)
    {
        var id = _db.FindActiveTaskIdByKey(key);
        if (id == null)
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Task key '{key}' not found among active tasks");
        return id;
    }

    private string BuildRefinementWindow(int windowSize)
    {
        try
        {
            var entries = _refContainer.Get(windowSize);
            if (entries.Count == 0)
                return "(暂无精炼结果)";

            var sb = new StringBuilder();
            foreach (var entry in entries)
                sb.AppendLine(entry);

            return sb.ToString().TrimEnd();
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Refinement window query failed (DB not ready): {ex.Message}");
            return "(暂无精炼结果)";
        }
    }

    private string BuildStoryProgressWindow(int windowSize)
    {
        try
        {
            var entries = _spContainer.Get(windowSize);
            if (entries.Count == 0)
                return "(暂无故事进展)";

            var sb = new StringBuilder();
            foreach (var entry in entries)
                sb.AppendLine(entry);

            return sb.ToString().TrimEnd();
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Story progress window query failed (DB not ready): {ex.Message}");
            return "(暂无故事进展)";
        }
    }

    private string BuildActiveTasksWindow(int limit)
    {
        try
        {
            var totalCount = _db.CountActiveTasks();
            if (totalCount == 0)
                return EmptyStateMarker;

            if (totalCount > limit)
                LogRedirector.Error("PowerWordRelive.LLMRequester",
                    $"Active task count ({totalCount}) exceeds window limit ({limit}), only showing first {limit} tasks");

            var entries = _db.GetActiveTasks(limit);

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

    private string BuildFinishedTasksWindow(int limit)
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