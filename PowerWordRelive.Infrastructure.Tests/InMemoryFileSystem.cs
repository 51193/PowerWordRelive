using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.Infrastructure.Tests;

public sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new();
    private readonly HashSet<string> _directories = new();

    public void AddFile(string path, string content) => _files[path] = content;
    public void AddDirectory(string path) => _directories.Add(path);

    public bool FileExists(string path) => _files.ContainsKey(path);
    public bool DirectoryExists(string path) => _directories.Contains(path);
    public void CreateDirectory(string path) => _directories.Add(path);

    public string[] GetFiles(string path, string pattern)
    {
        var extension = pattern.TrimStart('*');
        return _files.Keys
            .Where(k => k.StartsWith(path) && k.EndsWith(extension))
            .ToArray();
    }
    public void MoveFile(string source, string destination) { }
    public void DeleteFile(string path) => _files.Remove(path);
    public string[] ReadAllLines(string path) =>
        _files.TryGetValue(path, out var content) ? content.Split('\n') : [];
    public string ReadAllText(string path) =>
        _files.TryGetValue(path, out var content) ? content : "";

    public bool TryAcquireForProcessing(string filePath, out string processingPath)
    {
        processingPath = string.Empty;
        return false;
    }

    public bool TryReleaseProcessing(string processingPath, string originalPath) => false;
    public bool TryCompleteProcessing(string processingPath) => false;
}
