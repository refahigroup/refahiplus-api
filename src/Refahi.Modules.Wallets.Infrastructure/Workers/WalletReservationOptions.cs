namespace Refahi.Modules.Wallets.Infrastructure.Workers;

public sealed class WalletReservationOptions
{
    public int ExpirationMinutes { get; init; } = 30;
    public int AuditIntervalMinutes { get; init; } = 60;
    public int ReleaseBatchSize { get; init; } = 100;
}
