using System.Text;
using System.Text.RegularExpressions;
using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.Infrastructure.Prompt;

public class PromptAssembler
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{(.+?)\}\}", RegexOptions.Compiled);

    private readonly IFileSystem _fs;
    private readonly int _maxDepth;
    private readonly string _promptBaseDir;

    public PromptAssembler(IFileSystem fs, string promptBaseDir, int maxDepth = 10)
    {
        _fs = fs;
        _promptBaseDir = promptBaseDir;
        _maxDepth = maxDepth;
    }

    public string Assemble(string relativePath, Dictionary<string, string> variables)
    {
        return AssembleInternal(relativePath, variables, 0);
    }

    private string AssembleInternal(string relativePath, Dictionary<string, string> variables, int depth)
    {
        if (depth > _maxDepth)
            throw new InvalidPromptException(
                $"Prompt recursion depth exceeded ({_maxDepth}) at: {relativePath}");

        var fullPath = Path.GetFullPath(Path.Combine(_promptBaseDir, relativePath));

        if (!_fs.FileExists(fullPath))
            throw new InvalidPromptException($"Prompt template not found: {fullPath}");

        var content = _fs.ReadAllText(fullPath);

        return PlaceholderRegex.Replace(content, match =>
        {
            var inner = match.Groups[1].Value.Trim();

            if (inner.StartsWith("dir:"))
            {
                var dirPath = inner["dir:".Length..].Trim();
                if (string.IsNullOrWhiteSpace(dirPath))
                    throw new InvalidPromptException(
                        $"Empty dir path in template: {relativePath}");

                return AssembleInternal(dirPath, variables, depth + 1);
            }

            // Only .md files are loaded to avoid binary files,
            // editor temp files, or other non-text artifacts.
            if (inner.StartsWith("folder:"))
            {
                var folderRel = inner["folder:".Length..].Trim();
                if (string.IsNullOrWhiteSpace(folderRel))
                    throw new InvalidPromptException(
                        $"Empty folder path in template: {relativePath}");

                var folderPath = Path.GetFullPath(Path.Combine(_promptBaseDir, folderRel));

                if (!_fs.DirectoryExists(folderPath))
                    throw new InvalidPromptException(
                        $"Folder not found: {folderPath}");

                var files = _fs.GetFiles(folderPath, "*.md");
                Array.Sort(files);

                var sb = new StringBuilder();
                foreach (var file in files)
                {
                    var text = _fs.ReadAllText(file).TrimEnd();
                    // Hardcoded \n instead of Environment.NewLine because this
                    // output is consumed by LLM APIs and other platform-agnostic
                    // pipelines, not displayed to humans. For human-facing text,
                    // use Environment.NewLine.
                    sb.Append(text).Append('\n');
                }

                return sb.ToString();
            }

            if (inner.StartsWith("value:"))
            {
                var key = inner["value:".Length..].Trim();
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidPromptException(
                        $"Empty value key in template: {relativePath}");

                if (!variables.TryGetValue(key, out var value))
                    throw new InvalidPromptException(
                        $"Variable '{key}' not found in dictionary, referenced in: {relativePath}");

                return value;
            }

            throw new InvalidPromptException(
                $"Unknown placeholder type in template '{relativePath}': {{{{{inner}}}}}");
        });
    }
}