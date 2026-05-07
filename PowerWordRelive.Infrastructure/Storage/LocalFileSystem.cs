namespace PowerWordRelive.Infrastructure.Storage;

public class LocalFileSystem : IFileSystem
{
    private const string ProcessingExtension = ".processing";

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public string[] GetFiles(string path, string pattern)
    {
        return Directory.GetFiles(path, pattern);
    }

    public void MoveFile(string source, string destination)
    {
        File.Move(source, destination);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    public string[] ReadAllLines(string path)
    {
        return File.ReadAllLines(path);
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    public bool TryAcquireForProcessing(string filePath, out string processingPath)
    {
        processingPath = filePath + ProcessingExtension;
        try
        {
            File.Move(filePath, processingPath);
            return true;
        }
        catch
        {
            processingPath = string.Empty;
            return false;
        }
    }

    public bool TryReleaseProcessing(string processingPath, string originalPath)
    {
        try
        {
            if (File.Exists(processingPath))
                File.Move(processingPath, originalPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryCompleteProcessing(string processingPath)
    {
        try
        {
            File.Delete(processingPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}