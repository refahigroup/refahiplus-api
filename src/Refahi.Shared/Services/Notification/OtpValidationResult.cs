namespace Refahi.Shared.Services.Notification;

public record OtpValidationResult(
    bool IsValid,
    string? Receipt = null,
    OtpReceiptType? ReceiptType = null);
