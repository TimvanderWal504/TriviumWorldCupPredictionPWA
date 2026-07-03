namespace TriviumWorldCup.Api.Tests;

/// <summary>
/// TWC-65: WolverineFx and WolverineFx.Http PackageReferences were dead — zero usages anywhere
/// in src/**/*.cs. Removed from TriviumWorldCup.Api.csproj (the only project that referenced
/// them). Behavior-neutral; these guardrail tests just confirm the removal sticks.
/// </summary>
public class WolverineRemovalTests
{
    [Fact]
    public void NoWolverineUsingsOrReferences_AnywhereInApiSource()
    {
        var apiSrcDir = FindApiSrcDirectory();
        Assert.True(Directory.Exists(apiSrcDir), $"Could not locate API source directory at '{apiSrcDir}'.");

        var offendingFiles = new List<string>();
        foreach (var csFile in Directory.EnumerateFiles(apiSrcDir, "*.cs", SearchOption.AllDirectories))
        {
            var normalised = csFile.Replace('\\', '/');
            // Skip build output — generated files (e.g. MvcApplicationPartsAssemblyInfo.cs)
            // can transiently reference removed packages until the next clean build.
            if (normalised.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
                normalised.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
                continue;

            var content = File.ReadAllText(csFile);
            if (content.Contains("Wolverine", StringComparison.OrdinalIgnoreCase))
                offendingFiles.Add(csFile);
        }

        Assert.True(offendingFiles.Count == 0,
            $"Found Wolverine references in: {string.Join(", ", offendingFiles)}");
    }

    [Fact]
    public void ApiCsproj_HasNoWolverinePackageReference()
    {
        var csprojPath = Path.Combine(FindApiSrcDirectory(), "TriviumWorldCup.Api.csproj");
        Assert.True(File.Exists(csprojPath), $"Could not locate '{csprojPath}'.");

        var content = File.ReadAllText(csprojPath);
        Assert.DoesNotContain("WolverineFx", content, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindApiSrcDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "TriviumWorldCup.Api");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "src", "TriviumWorldCup.Api");
    }
}
