using System.Security.Cryptography;
using System.Text;

namespace Refahi.Modules.Media.Application.Contracts.External;

public sealed record ExternalMediaSource(Uri Url)
{
    public string FileName => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Url.AbsoluteUri))).ToLowerInvariant();
}

// These labels are only for administration; they never select transport or cache policies.
public sealed record ExternalMediaOrigin(string? Module = null, string? Provider = null);
public interface IExternalMediaService
{
    Task<string?> GetPublicUrlAsync(string sourceUrl, ExternalMediaOrigin? origin = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPublicUrlsAsync(IEnumerable<string> sourceUrls, ExternalMediaOrigin? origin = null, CancellationToken ct = default);
}
public interface IExternalMediaResolver
{
    Task<ExternalMediaSource?> ResolveAsync(Guid id, CancellationToken ct);
}
public interface IExternalMediaUrls
{
    string GetUrl(Guid id);
}
