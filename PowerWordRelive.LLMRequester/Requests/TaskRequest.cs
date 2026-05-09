using System.Text;
using Microsoft.Data.Sqlite;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Prompt;
using PowerWordRelive.LLMRequester.Core;
using PowerWordRelive.LLMRequester.Database;
using PowerWordRelive.LLMRequester.Parsing;
#if DEBUG
#endif

namespace PowerWordRelive.LLMRequester.Requests;

internal class TaskRequest : IRequest
{
    private const string SystemPromptFile = "prompts/task/task_system.md";
    private const string UserPromptFile = "prompts/task/task_user.md";

#if DEBUG
    private static readonly object TaskLogLock = new();
#endif

    private readonly string _apiUrl;
    private readonly string _token;
    private readonly LLMDatabase _db;
    private readonly RefinementContainer _refContainer;
    private readonly StoryProgressContainer _spContainer;
    private readonly TaskAccessor _taskAccessor;
    private readonly ConsistencyAccessor _consistencyAccessor;
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
        TaskAccessor taskAccessor,
        ConsistencyAccessor consistencyAccessor,
        PromptAssembler assembler,
        TaskConfig config,
        LlmApiClient apiClient)
    {
        _apiUrl = apiUrl;
        _token = token;
        _db = db;
        _refContainer = refContainer;
        _spContainer = spContainer;
        _taskAccessor = taskAccessor;
        _consistencyAccessor = consistencyAccessor;
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
        var activeTasksText = _taskAccessor.BuildActiveTasksText();
        var finishedTasksText = _taskAccessor.BuildFinishedTasksText(_config.FinishedTaskWindow);
        var consistencyText = _consistencyAccessor.BuildConsistencyTableText();

        var emptyVars = new Dictionary<string, string>();
        var userVars = new Dictionary<string, string>
        {
            ["refinement_window"] = refinementText,
            ["story_progress_window"] = storyProgressText,
            ["active_tasks"] = activeTasksText,
            ["finished_tasks"] = finishedTasksText,
            ["consistency_table"] = consistencyText
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
#if DEBUG
            AppendTaskLog(DateTime.Now, content, new List<TaskOperation>());
#endif
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

#if DEBUG
        AppendTaskLog(DateTime.Now, content, operations);
#endif

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

#if DEBUG
    private static void AppendTaskLog(DateTime localTime, string rawResponse,
        IReadOnlyList<TaskOperation> operations)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "task.log");
        var ts = localTime.ToString("yyyy-MM-dd HH:mm:ss");

        var opsLines = operations.Count == 0
            ? "  (EMPTY)"
            : string.Join('\n', operations.Select(o => o.Type switch
            {
                TaskOperation.OperationType.Append => $"  append: {o.Key} | {o.Value}",
                TaskOperation.OperationType.Remove => $"  remove: {o.Key}",
                TaskOperation.OperationType.Edit => $"  edit: {o.Key} | {o.Value}",
                TaskOperation.OperationType.Replace => $"  replace: {o.Key} -> {o.NewKey} | {o.Value}",
                TaskOperation.OperationType.Finish => $"  finish: {o.Key} | {o.Status}",
                _ => "  ?"
            }));

        var entry = $"""
                     === Task Operations @ {ts} ({operations.Count} ops) ===
                     --- LLM Raw Output ---
                     {rawResponse}
                     --- Parsed Operations ---
                     {opsLines}
                     ================================

                     """;

        lock (TaskLogLock)
        {
            File.AppendAllText(logPath, entry);
        }
    }
#endif
}
