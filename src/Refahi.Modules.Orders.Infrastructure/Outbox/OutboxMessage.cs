namespace Refahi.Modules.Orders.Infrastructure.Outbox;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;   // AssemblyQualifiedName
    public string EventData { get; set; } = string.Empty;   // JSON payload
    public DateTimeOffset OccurredAt { get; set; }
    public int RetryCount { get; set; }
    public string Status { get; set; } = OutboxMessageStatus.Pending;
    public DateTimeOffset? ProcessedAt { get; set; }        // null = هنوز پردازش نشده
    public string? Error { get; set; }
}

public static class OutboxMessageStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Processed = "Processed";
    public const string Failed = "Failed";
    public const string DeadLettered = "DeadLettered";
}
