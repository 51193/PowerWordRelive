namespace PowerWordRelive.Host.Process;

internal static class ProcessResolver
{
    public static string ResolveDllPath(string projectName)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", projectName, $"{projectName}.dll"));
    }
}