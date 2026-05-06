namespace PowerWordRelive.Infrastructure.Storage;

public class LocalFileSystem : IFileSystem
{
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
}