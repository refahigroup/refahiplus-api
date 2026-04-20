using MediatR;

namespace Refahi.Shared.Domain;

public interface IDomainEvent : INotification
{
    DateTimeOffset OccurredAt { get; }
}