using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Prompt;
using PowerWordRelive.LLMRequester.Core;
using PowerWordRelive.LLMRequester.Database;
using PowerWordRelive.LLMRequester.Parsing;
#if DEBUG
#endif

namespace PowerWordRelive.LLMRequester.Requests;

internal class RefinementRequest : IRequest
{
    private const string SystemPromptFile = "prompts/refine/refine_system.md";
    private const string UserPromptFile = "prompts/refine/refine_user.md";
    private const string EmptyStateMarker = "__EMPTY__";

    private static readonly Regex SpeakerIdNumberPattern =
        new(@"speaker_(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

#if DEBUG
    private static readonly object RefineLogLock = new();
#endif
    private readonly LlmApiClient _apiClient;

    private readonly string _apiUrl;
    private readonly PromptAssembler _assembler;
    private readonly RefinementConfig _config;
    private readonly ConsistencyAccessor _consistencyAccessor;
    private readonly RefinementContainer _container;
    private readonly LLMDatabase _db;
    private readonly RefinementParser _parser = new();
    private readonly string _token;

    public RefinementRequest(
        string apiUrl,
        string token,
        LLMDatabase db,
        RefinementContainer container,
        ConsistencyAccessor consistencyAccessor,
        PromptAssembler assembler,
        RefinementConfig config,
        LlmApiClient apiClient)
    {
        _apiUrl = apiUrl;
        _token = token;
        _db = db;
        _container = container;
        _consistencyAccessor = consistencyAccessor;
        _assembler = assembler;
        _config = config;
        _apiClient = apiClient;
    }

    public async Task Request()
    {
        if (!_db.TryEnsureRefinementTable())
            return;

        var dialogueText = BuildDialogueWindow(_config.DialogueWindow);
        var refinementText = BuildRefinementWindow(_config.RefinementWindow);
        var consistencyText = _consistencyAccessor.BuildConsistencyTableText();

        var emptyVars = new Dictionary<string, string>();
        var userVars = new Dictionary<string, string>
        {
            ["dialogue_window"] = dialogueText,
            ["refinement_window"] = refinementText,
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
                $"Prompt assembly failed: {ex.Message}");
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
                $"Refinement API call failed: {ex.Message}");
            return;
        }

        var content = response.Content.Trim();
        if (string.IsNullOrEmpty(content) || content == "EMPTY")
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                "Refinement returned EMPTY, no changes needed");
#if DEBUG
            AppendRefineLog(DateTime.Now, content, new List<IncrementalOperation>());
#endif
            return;
        }

        var operations = _parser.Parse(content);
        if (operations.Count == 0)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                "No valid refinement operations parsed");
            return;
        }

#if DEBUG
        AppendRefineLog(DateTime.Now, content, operations);
#endif

        LogRedirector.Info("PowerWordRelive.LLMRequester",
            $"Applying {operations.Count} refinement operation(s)");

        foreach (var op in operations)
            try
            {
                ApplyOperation(op);
            }
            catch (Exception ex)
            {
                LogRedirector.Error("PowerWordRelive.LLMRequester",
                    $"Failed to apply refinement operation {op.Type}: {ex.Message}");
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

    private string BuildDialogueWindow(int windowSize)
    {
        try
        {
            var entries = _db.GetLatestDialogues(windowSize);
            if (entries.Count == 0)
                return "(暂无对话记录)";

            var sb = new StringBuilder();
            foreach (var e in entries)
            {
                var displayName = ResolveDisplayName(e.SpeakerId, e.RoleName);
                sb.AppendLine($"{displayName}：{e.Text}");
            }

            return sb.ToString().TrimEnd();
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Dialogue window query failed (DB not ready): {ex.Message}");
            return "(暂无对话记录)";
        }
    }

    private string BuildRefinementWindow(int windowSize)
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
                $"Refinement window query failed (DB not ready): {ex.Message}");
            return EmptyStateMarker;
        }
    }

    private static string ResolveDisplayName(string speakerId, string? roleName)
    {
        if (!string.IsNullOrEmpty(roleName) && roleName != "__UNASSIGNED__" && roleName != "__UNKNOWN__")
            return roleName;

        var match = SpeakerIdNumberPattern.Match(speakerId);
        var number = match.Success ? match.Groups[1].Value : speakerId;
        return $"尚未确认{number}";
    }

#if DEBUG
    private static void AppendRefineLog(DateTime localTime, string rawResponse,
        IReadOnlyList<IncrementalOperation> operations)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "refine.log");
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
                     === Refine Operations @ {ts} ({operations.Count} ops) ===
                     --- LLM Raw Output ---
                     {rawResponse}
                     --- Parsed Operations ---
                     {opsLines}
                     ================================

                     """;

        lock (RefineLogLock)
        {
            File.AppendAllText(logPath, entry);
        }
    }
#endif
}