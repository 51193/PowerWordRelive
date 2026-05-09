using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.Infrastructure.Tests;

public sealed class InMemoryFileSystem : IFileSystem
{
    private readonly HashSet<string> _directories = new();
    private readonly Dictionary<string, string> _files = new();

    private static string N(string path)
    {
        return Path.GetFullPath(path);
    }

    public bool FileExists(string path)
    {
        return _files.ContainsKey(N(path));
    }

    public bool DirectoryExists(string path)
    {
        return _directories.Contains(N(path));
    }

    public void CreateDirectory(string path)
    {
        _directories.Add(N(path));
    }

    public string[] GetFiles(string path, string pattern)
    {
        var p = N(path);
        var extension = pattern.TrimStart('*');
        return _files.Keys
            .Where(k => k.StartsWith(p) && k.EndsWith(extension))
            .ToArray();
    }

    public void MoveFile(string source, string destination)
    {
    }

    public void DeleteFile(string path)
    {
        _files.Remove(N(path));
    }

    public string[] ReadAllLines(string path)
    {
        return _files.TryGetValue(N(path), out var content) ? content.Split('\n') : [];
    }

    public string ReadAllText(string path)
    {
        return _files.TryGetValue(N(path), out var content) ? content : "";
    }

    public long GetFileSize(string path)
    {
        return _files.TryGetValue(N(path), out var content) ? content.Length : 0;
    }

    public bool TryAcquireForProcessing(string filePath, out string processingPath)
    {
        processingPath = string.Empty;
        return false;
    }

    public bool TryReleaseProcessing(string processingPath, string originalPath)
    {
        return false;
    }

    public bool TryCompleteProcessing(string processingPath)
    {
        return false;
    }

    public void AddFile(string path, string content)
    {
        _files[N(path)] = content;
    }

    public void AddDirectory(string path)
    {
        _directories.Add(N(path));
    }
}
