namespace Refahi.Shared.Services.Notification;

public record SendOtpResult(
    string ReferenceCode,
    DateTime ExpiresAt);
