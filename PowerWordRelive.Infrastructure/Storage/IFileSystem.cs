namespace PowerWordRelive.Infrastructure.Storage;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    string[] GetFiles(string path, string pattern);
    void MoveFile(string source, string destination);
    void DeleteFile(string path);
    string[] ReadAllLines(string path);
    string ReadAllText(string path);
    bool TryAcquireForProcessing(string filePath, out string processingPath);
    bool TryReleaseProcessing(string processingPath, string originalPath);
    bool TryCompleteProcessing(string processingPath);
}