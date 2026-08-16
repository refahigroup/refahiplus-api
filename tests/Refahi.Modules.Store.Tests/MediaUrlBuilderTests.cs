using Refahi.Shared.Services.Path;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class MediaUrlBuilderTests
{
    [Theory]
    [InlineData(
        "images/2026/08/16/file.jpg",
        "https://media.tochalvip.ir/images/2026/08/16/file.jpg"
    )]
    [InlineData(
        "https://tochal_api:8080/api/images/2026/08/16/file.jpg",
        "https://media.tochalvip.ir/images/2026/08/16/file.jpg"
    )]
    [InlineData(
        "https://media.tochalvip.ir/images/2026/08/16/file.jpg",
        "https://media.tochalvip.ir/images/2026/08/16/file.jpg"
    )]
    public void MakeAbsolute_UsesConfiguredMediaHost(string input, string expected)
    {
        var result = MediaUrlBuilder.MakeAbsolute("https://media.tochalvip.ir", input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void MakeAbsolute_PreservesConfiguredBasePath()
    {
        var result = MediaUrlBuilder.MakeAbsolute(
            "https://localhost:5001/media-files",
            "/media-files/images/file.jpg"
        );

        Assert.Equal("https://localhost:5001/media-files/images/file.jpg", result);
    }
}
