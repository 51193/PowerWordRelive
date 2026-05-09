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

internal class StoryProgressRequest : IRequest
{
    private const string SystemPromptFile = "prompts/story_progress/story_progress_system.md";
    private const string UserPromptFile = "prompts/story_progress/story_progress_user.md";
    private const string EmptyStateMarker = "__EMPTY__";

#if DEBUG
    private static readonly object StoryProgressLogLock = new();
#endif
    private readonly LlmApiClient _apiClient;

    private readonly string _apiUrl;
    private readonly PromptAssembler _assembler;
    private readonly StoryProgressConfig _config;
    private readonly ConsistencyAccessor _consistencyAccessor;
    private readonly StoryProgressContainer _container;
    private readonly LLMDatabase _db;
    private readonly StoryProgressParser _parser = new();
    private readonly RefinementContainer _refContainer;
    private readonly string _token;

    public StoryProgressRequest(
        string apiUrl,
        string token,
        LLMDatabase db,
        StoryProgressContainer container,
        RefinementContainer refContainer,
        ConsistencyAccessor consistencyAccessor,
        PromptAssembler assembler,
        StoryProgressConfig config,
        LlmApiClient apiClient)
    {
        _apiUrl = apiUrl;
        _token = token;
        _db = db;
        _container = container;
        _refContainer = refContainer;
        _consistencyAccessor = consistencyAccessor;
        _assembler = assembler;
        _config = config;
        _apiClient = apiClient;
    }

    public async Task Request()
    {
        if (!_db.TryEnsureStoryProgressTable())
            return;

        var refinementText = BuildRefinementWindow(_config.RefinementWindow);
        var storyProgressText = BuildStoryProgressWindow(_config.StoryProgressWindow);
        var consistencyText = _consistencyAccessor.BuildConsistencyTableText();

        var emptyVars = new Dictionary<string, string>();
        var userVars = new Dictionary<string, string>
        {
            ["refinement_window"] = refinementText,
            ["story_progress_window"] = storyProgressText,
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
                $"Story progress prompt assembly failed: {ex.Message}");
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
                $"Story progress API call failed: {ex.Message}");
            return;
        }

        var content = response.Content.Trim();
        if (string.IsNullOrEmpty(content) || content == "EMPTY")
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                "Story progress returned EMPTY, no changes needed");
#if DEBUG
            AppendStoryProgressLog(DateTime.Now, content, new List<IncrementalOperation>());
#endif
            return;
        }

        var operations = _parser.Parse(content);
        if (operations.Count == 0)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                "No valid story progress operations parsed");
            return;
        }

        LogRedirector.Info("PowerWordRelive.LLMRequester",
            $"Applying {operations.Count} story progress operation(s)");

#if DEBUG
        AppendStoryProgressLog(DateTime.Now, content, operations);
#endif

        foreach (var op in operations)
            try
            {
                ApplyOperation(op);
            }
            catch (Exception ex)
            {
                LogRedirector.Error("PowerWordRelive.LLMRequester",
                    $"Failed to apply story progress operation {op.Type}: {ex.Message}");
            }
    }

    private void ApplyOperation(IncrementalOperation op)
    {
        switch (op.Type)
        {
            case IncrementalOperation.OperationType.Append:
                _container.Add(op.Content!);
                break;

            case IncrementalOperation.OperationType.Insert:
                _container.Add(op.DisplayIndex!.Value, op.Content!);
                break;

            case IncrementalOperation.OperationType.Edit:
                _container.Edit(op.DisplayIndex!.Value, op.Content!);
                break;

            case IncrementalOperation.OperationType.Remove:
                _container.Remove(op.DisplayIndex!.Value);
                break;
        }
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
            var entries = _container.Get(windowSize);
            if (entries.Count == 0)
                return EmptyStateMarker;

            var sb = new StringBuilder();
            for (var i = 0; i < entries.Count; i++)
                sb.AppendLine($"{i + 1}. {entries[i]}");

            return sb.ToString().TrimEnd();
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Story progress window query failed (DB not ready): {ex.Message}");
            return EmptyStateMarker;
        }
    }

#if DEBUG
    private static void AppendStoryProgressLog(DateTime localTime, string rawResponse,
        IReadOnlyList<IncrementalOperation> operations)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "story_progress.log");
        var ts = localTime.ToString("yyyy-MM-dd HH:mm:ss");

        var opsLines = operations.Count == 0
            ? "  (EMPTY)"
            : string.Join('\n', operations.Select(o => o.Type switch
            {
                IncrementalOperation.OperationType.Append => $"  append: {o.Content}",
                IncrementalOperation.OperationType.Insert => $"  insert @{o.DisplayIndex}: {o.Content}",
                IncrementalOperation.OperationType.Edit => $"  edit @{o.DisplayIndex}: {o.Content}",
                IncrementalOperation.OperationType.Remove => $"  remove @{o.DisplayIndex}",
                _ => "  ?"
            }));

        var entry = $"""
                     === StoryProgress Operations @ {ts} ({operations.Count} ops) ===
                     --- LLM Raw Output ---
                     {rawResponse}
                     --- Parsed Operations ---
                     {opsLines}
                     ================================

                     """;

        lock (StoryProgressLogLock)
        {
            File.AppendAllText(logPath, entry);
        }
    }
#endif
}