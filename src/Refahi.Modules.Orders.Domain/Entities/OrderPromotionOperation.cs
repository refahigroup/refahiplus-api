namespace Refahi.Modules.Orders.Domain.Entities;

public sealed class OrderPromotionOperation
{
    private OrderPromotionOperation() { }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string IdempotencyKey { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string Kind { get; private set; } = "";
    public string? CodeCiphertext { get; private set; }
    public string? ResultJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public static OrderPromotionOperation Create(Guid orderId, string key, string fingerprint, string kind, string? ciphertext, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), OrderId = orderId, IdempotencyKey = key, Fingerprint = fingerprint, Kind = kind, CodeCiphertext = ciphertext, CreatedAt = now };
    public void Complete(string result) { ResultJson = result; CodeCiphertext = null; }
}
