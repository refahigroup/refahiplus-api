namespace Refahi.Modules.Media.Application.Contracts.External;

// Only background synchronization calls EnsureAsync. URL generation and serving never fetch upstream.
public interface IPersistentExternalMedia
{
    Task<Guid> EnsureAsync(string sourceUrl, ExternalMediaOrigin origin, CancellationToken ct);
    string GetUrl(Guid id);
    bool Exists(Guid id);
}
