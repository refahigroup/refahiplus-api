using System.Net;
using Refahi.Modules.Media.Infrastructure.Services;
using Xunit;
namespace Refahi.Modules.Media.Tests;
public class ExternalMediaSecurityTests
{
    [Theory]
    [InlineData("127.0.0.1")][InlineData("10.0.0.1")][InlineData("169.254.169.254")]
    [InlineData("::1")][InlineData("::ffff:127.0.0.1")][InlineData("fc00::1")]
    [InlineData("100.64.0.1")][InlineData("192.168.1.1")][InlineData("172.31.0.1")]
    public void RejectsNonPublicAddresses(string ip) => Assert.False(ExternalMedia.PublicAddress(IPAddress.Parse(ip)));
    [Fact] public void AllowsPublicAddress() => Assert.True(ExternalMedia.PublicAddress(IPAddress.Parse("8.8.8.8")));
    [Fact] public void RejectsRiffWithoutWebpChunk() => Assert.Throws<ExternalMediaException>(() => ExternalMedia.Validate("RIFF1234WAVE"u8.ToArray(), "image/webp"));
    [Fact] public void AcceptsWebpHeader() => ExternalMedia.Validate("RIFF1234WEBP"u8.ToArray(), "image/webp");
    [Fact] public void RejectsHtml() => Assert.Throws<ExternalMediaException>(() => ExternalMedia.Validate("<html>"u8.ToArray(), "image/jpeg"));
}
