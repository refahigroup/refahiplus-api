using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class FlightsDependencyTests
{
    [Fact]
    public void FlightsProjects_DoNotReferenceWalletProjects()
    {
        var projectFiles = Directory.EnumerateFiles(
            Path.Combine(TestPaths.RepositoryRoot, "src"),
            "Refahi.Modules.Flights.*.csproj",
            SearchOption.AllDirectories);

        foreach (var projectFile in projectFiles)
        {
            var projectXml = File.ReadAllText(projectFile);
            Assert.DoesNotContain("Refahi.Modules.Wallets", projectXml, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FlightsSource_DoesNotUseWalletNamespace()
    {
        var sourceFiles = Directory.EnumerateFiles(
                Path.Combine(TestPaths.RepositoryRoot, "src"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => path.Contains("Refahi.Modules.Flights.", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var sourceFile in sourceFiles)
        {
            var source = File.ReadAllText(sourceFile);
            Assert.DoesNotContain("Refahi.Modules.Wallets", source, StringComparison.OrdinalIgnoreCase);
        }
    }
}
