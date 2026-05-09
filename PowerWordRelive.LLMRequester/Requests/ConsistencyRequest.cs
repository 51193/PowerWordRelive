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

internal class ConsistencyRequest : IRequest
{
    private const string SystemPromptFile = "prompts/consistency/consistency_system.md";
    private const string UserPromptFile = "prompts/consistency/consistency_user.md";

#if DEBUG
    private static readonly object ConsistencyLogLock = new();
#endif
    private readonly LlmApiClient _apiClient;

    private readonly string _apiUrl;
    private readonly PromptAssembler _assembler;
    private readonly ConsistencyConfig _config;
    private readonly ConsistencyAccessor _consistencyAccessor;
    private readonly LLMDatabase _db;
    private readonly ConsistencyParser _parser = new();
    private readonly RefinementContainer _refContainer;
    private readonly StoryProgressContainer _spContainer;
    private readonly TaskAccessor _taskAccessor;
    private readonly string _token;

    public ConsistencyRequest(
        string apiUrl,
        string token,
        LLMDatabase db,
        RefinementContainer refContainer,
        StoryProgressContainer spContainer,
        TaskAccessor taskAccessor,
        ConsistencyAccessor consistencyAccessor,
        PromptAssembler assembler,
        ConsistencyConfig config,
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
        if (!_db.TryEnsureConsistencyTable())
            return;

        var refinementText = BuildRefinementWindow(_config.RefinementWindow);
        var storyProgressText = BuildStoryProgressWindow(_config.StoryProgressWindow);
        var activeTasksText = _taskAccessor.BuildActiveTasksText();
        var consistencyTableText = _consistencyAccessor.BuildConsistencyTableText();

        var emptyVars = new Dictionary<string, string>();
        var userVars = new Dictionary<string, string>
        {
            ["refinement_window"] = refinementText,
            ["story_progress_window"] = storyProgressText,
            ["active_tasks"] = activeTasksText,
            ["consistency_table"] = consistencyTableText
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
                $"Consistency prompt assembly failed: {ex.Message}");
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
                $"Consistency API call failed: {ex.Message}");
            return;
        }

        var content = response.Content.Trim();
        if (string.IsNullOrEmpty(content) || content == "EMPTY")
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                "Consistency returned EMPTY, no changes needed");
#if DEBUG
            AppendConsistencyLog(DateTime.Now, content, new List<ConsistencyOperation>());
#endif
            return;
        }

        var operations = _parser.Parse(content);
        if (operations.Count == 0)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                "No valid consistency operations parsed");
            return;
        }

        LogRedirector.Info("PowerWordRelive.LLMRequester",
            $"Applying {operations.Count} consistency operation(s)");

#if DEBUG
        AppendConsistencyLog(DateTime.Now, content, operations);
#endif

        foreach (var op in operations)
            try
            {
                ApplyOperation(op);
            }
            catch (Exception ex)
            {
                LogRedirector.Error("PowerWordRelive.LLMRequester",
                    $"Failed to apply consistency operation {op.Type}: {ex.Message}");
            }
    }

    private void ApplyOperation(ConsistencyOperation op)
    {
        switch (op.Type)
        {
            case ConsistencyOperation.OperationType.Append:
            {
                var existingId = _db.FindActiveConsistencyIdByName(op.Name!);
                if (existingId != null)
                {
                    LogRedirector.Warn("PowerWordRelive.LLMRequester",
                        $"Consistency append skipped: name '{op.Name}' already exists");
                    return;
                }

                _db.InsertConsistencyEntry(op.Name!, op.Detail!);
                break;
            }

            case ConsistencyOperation.OperationType.Remove:
            {
                var id = ResolveActiveConsistencyId(op.Name!);
                if (id == null)
                    return;
                _db.SoftDeleteConsistencyEntry(id.Value);
                break;
            }

            case ConsistencyOperation.OperationType.Edit:
            {
                var id = ResolveActiveConsistencyId(op.Name!);
                if (id == null)
                    return;
                _db.UpdateConsistencyEntry(id.Value, op.Name!, op.Detail!);
                break;
            }
        }
    }

    private int? ResolveActiveConsistencyId(string name)
    {
        var id = _db.FindActiveConsistencyIdByName(name);
        if (id == null)
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Consistency name '{name}' not found among active entries");
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
    private static void AppendConsistencyLog(DateTime localTime, string rawResponse,
        IReadOnlyList<ConsistencyOperation> operations)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "consistency.log");
        var ts = localTime.ToString("yyyy-MM-dd HH:mm:ss");

        var opsLines = operations.Count == 0
            ? "  (EMPTY)"
            : string.Join('\n', operations.Select(o => o.Type switch
            {
                ConsistencyOperation.OperationType.Append => $"  append: {o.Name} | {o.Detail}",
                ConsistencyOperation.OperationType.Remove => $"  remove: {o.Name}",
                ConsistencyOperation.OperationType.Edit => $"  edit: {o.Name} | {o.Detail}",
                _ => "  ?"
            }));

        var entry = $"""
                     === Consistency Operations @ {ts} ({operations.Count} ops) ===
                     --- LLM Raw Output ---
                     {rawResponse}
                     --- Parsed Operations ---
                     {opsLines}
                     ================================

                     """;

        lock (ConsistencyLogLock)
        {
            File.AppendAllText(logPath, entry);
        }
    }
#endif
}