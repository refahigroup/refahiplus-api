namespace Refahi.Modules.Media.Domain.Aggregates;

public enum ExternalMediaStatus { NotCached, Cached, Failed }

public sealed class ExternalMediaSource
{
    private ExternalMediaSource() { }
    public Guid Id { get; private set; }
    public string SourceUrl { get; private set; } = "";
    public string FileName { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastAccessedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? RefreshAfter { get; private set; }
    public DateTime StateChangedAt { get; private set; }
    public ExternalMediaStatus Status { get; private set; }
    public static ExternalMediaSource Create(string url, string hash, DateTime now) => new()
    {
        Id = Guid.NewGuid(), SourceUrl = url, FileName = hash, CreatedAt = now,
        StateChangedAt = now, Status = ExternalMediaStatus.NotCached
    };
}

public sealed class ExternalMediaOrigin
{
    private ExternalMediaOrigin() { }
    public Guid SourceId { get; private set; }
    public string Module { get; private set; } = "";
    public string Provider { get; private set; } = "";
    public static ExternalMediaOrigin Create(Guid sourceId, string? module, string? provider) => new()
    { SourceId = sourceId, Module = module ?? "", Provider = provider ?? "" };
}
