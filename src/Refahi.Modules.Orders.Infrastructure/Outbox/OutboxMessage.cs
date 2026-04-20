namespace Refahi.Modules.Orders.Infrastructure.Outbox;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;   // AssemblyQualifiedName
    public string EventData { get; set; } = string.Empty;   // JSON
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }        // null = هنوز پردازش نشده
    public string? Error { get; set; }
}
