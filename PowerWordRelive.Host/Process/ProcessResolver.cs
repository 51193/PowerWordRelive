namespace PowerWordRelive.Host.Process;

public static class ProcessResolver
{
    public static string ResolveDllPath(string projectName)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", projectName, $"{projectName}.dll"));
    }
}