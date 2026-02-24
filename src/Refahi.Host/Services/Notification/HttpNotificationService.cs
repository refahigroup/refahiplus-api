using Refahi.Host.Services.Notification.Dtos;
using Refahi.Shared.Services.Notification;

namespace Refahi.Host.Services.Notification;

/// <summary>
/// HTTP-based implementation of INotificationService using external OTP API
/// </summary>
public class HttpNotificationService : INotificationService
{
    private readonly OtpApiClient _otpApiClient;
    private readonly MessageApiClient _messageApiClient;
    private readonly ILogger<HttpNotificationService> _logger;

    public HttpNotificationService(
        OtpApiClient otpApiClient,
        MessageApiClient messageApiClient,
        ILogger<HttpNotificationService> logger)
    {
        _otpApiClient = otpApiClient;
        _messageApiClient = messageApiClient;
        _logger = logger;
    }

    public async Task<SendOtpResult> SendOtp(
        string receipt, 
        OtpReceiptType type, 
        OtpType otpType, 
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new GenerateOtpRequest
            {
                Destination = receipt,
                Type = MapReceiptType(type),
                Purpose = MapOtpType(otpType),
                TtlMinutes = 5, // Default 5 minutes
                Length = 6      // Default 6 digits
            };

            var response = await _otpApiClient.GenerateAsync(request, cancellationToken);

            _logger.LogInformation(
                "OTP sent via external API. Receipt={Receipt}, Type={Type}, Purpose={Purpose}, ReferenceCode={ReferenceCode}, ExpiresAt={ExpiresAt}",
                MaskReceipt(receipt), 
                type, 
                otpType, 
                response.ReferenceCode,
                response.ExpiresAt);

            return new SendOtpResult(response.ReferenceCode, response.ExpiresAt);
        }
        catch (OtpApiException ex)
        {
            _logger.LogError(ex, 
                "Failed to send OTP via external API. Receipt={Receipt}, Type={Type}",
                MaskReceipt(receipt), 
                type);

            throw new InvalidOperationException($"Failed to send OTP: {ex.Message}", ex);
        }
    }

    public async Task<OtpValidationResult> ValidateOtp(
        string referenceCode, 
        string otp, 
        OtpType otpType, 
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new ValidateOtpRequest
            {
                ReferenceCode = referenceCode,
                Code = otp
            };

            var response = await _otpApiClient.ValidateAsync(request, cancellationToken);

            if (!response.IsValid)
            {
                _logger.LogWarning(
                    "OTP validation failed. ReferenceCode={ReferenceCode}, Message={Message}, AttemptsRemaining={AttemptsRemaining}",
                    referenceCode, 
                    response.Message, 
                    response.AttemptsRemaining);

                return new OtpValidationResult(false);
            }

            // API now returns destination info in successful validation responses
            if (string.IsNullOrEmpty(response.Destination) || string.IsNullOrEmpty(response.DestinationType))
            {
                _logger.LogWarning(
                    "OTP validated successfully but destination info missing. ReferenceCode={ReferenceCode}",
                    referenceCode);

                return new OtpValidationResult(true);
            }

            var receiptType = ParseReceiptType(response.DestinationType);

            _logger.LogInformation(
                "OTP validated successfully via external API. ReferenceCode={ReferenceCode}, Receipt={Receipt}, Type={Type}",
                referenceCode,
                MaskReceipt(response.Destination),
                receiptType);

            return new OtpValidationResult(true, response.Destination, receiptType);
        }
        catch (OtpApiException ex)
        {
            _logger.LogError(ex, 
                "Failed to validate OTP via external API. ReferenceCode={ReferenceCode}",
                referenceCode);

            // Return invalid result instead of throwing (validation failed gracefully)
            return new OtpValidationResult(false);
        }
    }

    public async Task SendSms(
        string[] phoneNumbers,
        string body,
        string? sender = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new SendMessageRequest
            {
                Sms = new SendSmsRequest
                {
                    PhoneNumbers = phoneNumbers,
                    Body = body,
                    Sender = sender
                }
            };

            await _messageApiClient.SendMessageAsync(request, cancellationToken);

            _logger.LogInformation(
                "SMS sent successfully via external API. Recipients={Count}, Sender={Sender}",
                phoneNumbers.Length,
                sender ?? "default");
        }
        catch (MessageApiException ex)
        {
            _logger.LogError(ex,
                "Failed to send SMS via external API. Recipients={Count}",
                phoneNumbers.Length);

            throw new InvalidOperationException($"Failed to send SMS: {ex.Message}", ex);
        }
    }

    private static string MapReceiptType(OtpReceiptType type) => type switch
    {
        OtpReceiptType.Sms => "sms",
        OtpReceiptType.Email => "email",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown receipt type")
    };

    private static OtpReceiptType ParseReceiptType(string type) => type?.ToLowerInvariant() switch
    {
        "mobile" => OtpReceiptType.Sms,
        "sms" => OtpReceiptType.Sms,        // Legacy support
        "email" => OtpReceiptType.Email,
        _ => throw new ArgumentException($"Unknown destination type: {type}", nameof(type))
    };

    private static string MapOtpType(OtpType type) => type switch
    {
        OtpType.SignIn => "login",
        OtpType.SignUp => "signup",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown OTP type")
    };

    private static string MaskReceipt(string receipt)
    {
        if (string.IsNullOrEmpty(receipt) || receipt.Length < 4)
            return "***";

        // Mask mobile number: 0912***4321
        if (receipt.StartsWith("09") && receipt.Length == 11)
        {
            return $"{receipt[..4]}***{receipt[^4..]}";
        }

        // Mask email: us***@example.com
        if (receipt.Contains('@'))
        {
            var parts = receipt.Split('@');
            var username = parts[0];
            var domain = parts[1];
            var maskedUsername = username.Length > 2 
                ? $"{username[..2]}***" 
                : "***";
            return $"{maskedUsername}@{domain}";
        }

        // Default masking
        return $"{receipt[..2]}***{receipt[^2..]}";
    }
}
