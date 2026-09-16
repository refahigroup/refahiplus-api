namespace Refahi.Modules.PaymentGateway.Application.Contracts.Configuration;

public sealed class FundingSafetyOptions
{
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string[] AllowedReturnHosts { get; set; } = [];
    public bool AllowRelativeReturnUrls { get; set; } = true;
    public int ProcessingLeaseSeconds { get; set; } = 120;
    public int RecoveryPollSeconds { get; set; } = 30;
    public int RecoveryBatchSize { get; set; } = 50;
    public int RecoveryBaseDelaySeconds { get; set; } = 30;
    public int RecoveryMaxAgeHours { get; set; } = 72;
    public int CallbackPermitLimit { get; set; } = 60;
    public int CallbackWindowSeconds { get; set; } = 60;
    public long CallbackMaxBodyBytes { get; set; } = 65536;
}
