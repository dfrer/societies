using Xunit;

namespace Societies.SnowGlobe.Tests;

/// <summary>Finds the repository by stable product markers, never by a scoped AGENTS.md file.</summary>
internal static class RepositoryTestPaths
{
    internal static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        for (int depth = 0; depth < 16 && current is not null; depth++, current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "CURRENT_BUILD.md"))
                && File.Exists(Path.Combine(
                    current.FullName,
                    "labs",
                    "Societies.SnowGlobe",
                    "Societies.SnowGlobe.csproj")))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("repository_root_not_found");
    }

    internal static string Resolve(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Path.Combine(
            FindRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}

/// <summary>
/// Runs an exact evidence-pinning test only when its operator-retained artifact is present.
/// Missing bytes are reported as unavailable rather than fabricated; all synthetic contract,
/// tamper, routing, durability, and schema tests remain mandatory on every clean runner.
/// When an artifact exists, the original strict validator runs without fallback evidence.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class HistoricalEvidenceFactAttribute : FactAttribute
{
    public HistoricalEvidenceFactAttribute(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string path;
        try
        {
            path = RepositoryTestPaths.Resolve(relativePath);
        }
        catch (DirectoryNotFoundException)
        {
            Skip = "Repository root unavailable; optional historical evidence was not validated.";
            return;
        }

        if (!File.Exists(path))
            Skip = $"Optional operator-retained evidence is not present: {relativePath}";
    }
}
