namespace Refahi.Modules.Charge.Domain.Aggregates;

public sealed class ChargePin
{
    private ChargePin() { }
    public Guid Id { get; private set; }
    public Guid ChargeRequestId { get; private set; }
    public string EncryptedSerial { get; private set; } = string.Empty;
    public string EncryptedCode { get; private set; } = string.Empty;
    public long AmountMinor { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static ChargePin Create(Guid requestId, string encryptedSerial, string encryptedCode, long amountMinor, DateTime nowUtc)
        => new() { Id = Guid.NewGuid(), ChargeRequestId = requestId, EncryptedSerial = encryptedSerial, EncryptedCode = encryptedCode, AmountMinor = amountMinor, CreatedAt = nowUtc };
}
