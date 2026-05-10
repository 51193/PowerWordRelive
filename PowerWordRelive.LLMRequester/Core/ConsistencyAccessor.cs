using System.Text;
using Microsoft.Data.Sqlite;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.LLMRequester.Database;

namespace PowerWordRelive.LLMRequester.Core;

public class ConsistencyAccessor
{
    private const string EmptyStateMarker = "__EMPTY__";
    private readonly int _consistencyLimit;

    private readonly LLMDatabase _db;

    public ConsistencyAccessor(LLMDatabase db, int consistencyLimit)
    {
        _db = db;
        _consistencyLimit = consistencyLimit;
    }

    public string BuildConsistencyTableText()
    {
        try
        {
            var totalCount = _db.CountActiveConsistencyEntries();
            if (totalCount == 0)
                return EmptyStateMarker;

            if (totalCount > _consistencyLimit)
                LogRedirector.Error("PowerWordRelive.LLMRequester",
                    $"Active consistency entry count ({totalCount}) exceeds window limit ({_consistencyLimit}), only showing first {_consistencyLimit} entries");

            var entries = _db.GetActiveConsistencyEntries(_consistencyLimit);

            var sb = new StringBuilder();
            foreach (var (_, name, detail, tag) in entries)
                sb.AppendLine($"[{tag}] {name}：{detail}");

            return sb.ToString().TrimEnd();
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Consistency table query failed (DB not ready): {ex.Message}");
            return EmptyStateMarker;
        }
    }
}