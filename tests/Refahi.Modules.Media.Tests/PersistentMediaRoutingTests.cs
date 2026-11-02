using Xunit;
namespace Refahi.Modules.Media.Tests;
public sealed class PersistentMediaRoutingTests
{
    [Fact]
    public void Persistent_images_are_mapped_at_the_root_matching_generated_public_urls()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Refahi.Backend.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory.FullName, "src", "Refahi.Modules.Media.Api", "DI.cs"));
        Assert.Contains("new Endpoints.PersistentExternalMediaEndpoint().Map(app)", source);
        Assert.Contains("t != typeof(Endpoints.PersistentExternalMediaEndpoint)", source);
    }
}
