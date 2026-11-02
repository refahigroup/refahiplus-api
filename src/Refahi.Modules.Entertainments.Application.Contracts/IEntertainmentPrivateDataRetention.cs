namespace Refahi.Modules.Entertainments.Application.Contracts;
public interface IEntertainmentPrivateDataRetention
{
    Task PruneAsync(DateTimeOffset cutoff, CancellationToken ct);
}
