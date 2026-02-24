namespace Refahi.Shared.Domain;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}