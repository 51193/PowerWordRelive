namespace PowerWordRelive.Host.Models;

internal record ProcessDefinition(
    string Name,
    string ProjectName,
    string[] Domains,
    string? ExtraArgs = null,
    Dictionary<string, Dictionary<string, string>>? InjectedConfig = null);
