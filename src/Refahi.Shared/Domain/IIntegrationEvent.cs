using MediatR;

namespace Refahi.Shared.Domain;

/// <summary>
/// Integration Events — رویدادهایی که از مرز ماژول عبور می‌کنند
/// در Application.Contracts هر ماژول تعریف می‌شوند (نه در Domain)
/// </summary>
public interface IIntegrationEvent : INotification
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}
