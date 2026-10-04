using System.Reflection;

namespace Janitor.Factory;

public static class JanitorBuildInfo
{
    public static string DisplayVersion
    {
        get
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(JanitorBuildInfo).Assembly;
            string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString(4)
                ?? "unknown";
            string? buildType = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "BuildType")?.Value;

            return string.IsNullOrWhiteSpace(buildType) ? version : $"{version} ({buildType})";
        }
    }
}
