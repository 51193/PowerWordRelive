using System.Reflection;

namespace PowerWordRelive.Infrastructure;

public static class AppInfo
{
    public const string Name = "PowerWordRelive";
    public const string Description = "TRPG 跑团 AI 记录员";

    public static string Version => typeof(AppInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "0.0.0";
}