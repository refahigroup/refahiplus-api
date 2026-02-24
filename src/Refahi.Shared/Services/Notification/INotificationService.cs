namespace Refahi.Shared.Services.Notification;

public interface INotificationService
{
    /// <summary>
    /// Send OTP to the specified receipt (phone number or email)
    /// </summary>
    /// <returns>OTP send result with reference code and expiration time</returns>
    Task<SendOtpResult> SendOtp(string receipt, OtpReceiptType type, OtpType otpType, CancellationToken cancellationToken);

    /// <summary>
    /// Validate OTP using reference code
    /// </summary>
    Task<OtpValidationResult> ValidateOtp(string referenceCode, string otp, OtpType otpType, CancellationToken cancellationToken);

    /// <summary>
    /// Send SMS message to one or more phone numbers
    /// </summary>
    /// <param name="phoneNumbers">Recipient phone numbers</param>
    /// <param name="body">Message body (supports {{time}} placeholder)</param>
    /// <param name="sender">Optional sender number/name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendSms(string[] phoneNumbers, string body, string? sender = null, CancellationToken cancellationToken = default);
}
