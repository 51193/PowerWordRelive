namespace PowerWordRelive.Host.Models;

internal record ProcessDefinition(string Name, string ProjectName, string[] Domains, string? ExtraArgs = null);
