using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Refahi.Shared.Services.Notification;

namespace Refahi.Host.Services.Notification;

/// <summary>
/// In-memory implementation of INotificationService for development/testing
/// Stores OTPs in memory with expiration and rate limiting
/// </summary>
public class InMemoryNotificationService : INotificationService
{
    private readonly ILogger<InMemoryNotificationService> _logger;
    private readonly ConcurrentDictionary<string, OtpEntry> _otpStore = new();
    private readonly ConcurrentDictionary<string, List<DateTime>> _sendHistory = new();
    private readonly TimeSpan _otpExpiration = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _rateLimitWindow = TimeSpan.FromMinutes(10);
    private readonly int _maxOtpPerReceipt = 3;
    private readonly int _maxValidationAttempts = 3;

    public InMemoryNotificationService(ILogger<InMemoryNotificationService> logger)
    {
        _logger = logger;
    }

    public Task<SendOtpResult> SendOtp(string receipt, OtpReceiptType type, OtpType otpType, CancellationToken cancellationToken)
    {
        // Check rate limiting: max sends per receipt in time window
        CleanupOldSendHistory();
        
        if (!_sendHistory.TryGetValue(receipt, out var sendTimes))
        {
            sendTimes = new List<DateTime>();
            _sendHistory[receipt] = sendTimes;
        }

        var recentSends = sendTimes.Count(t => DateTime.UtcNow - t < _rateLimitWindow);
        if (recentSends >= _maxOtpPerReceipt)
        {
            _logger.LogWarning(
                "OTP rate limit exceeded for {Receipt}. {Count} OTPs sent in last {Window} minutes",
                receipt, recentSends, _rateLimitWindow.TotalMinutes);
            
            throw new InvalidOperationException(
                $"Rate limit exceeded. Maximum {_maxOtpPerReceipt} OTP requests allowed per {_rateLimitWindow.TotalMinutes} minutes.");
        }

        // Generate OTP code (6 digits)
        var otpCode = GenerateOtpCode();

        // Generate reference code (16 char hex string)
        var referenceCode = Guid.NewGuid().ToString("N")[..16];

        // Store OTP with expiration
        var entry = new OtpEntry
        {
            OtpCode = otpCode,
            Receipt = receipt,
            ReceiptType = type,
            OtpType = otpType,
            ExpiresAt = DateTime.UtcNow.Add(_otpExpiration),
            IsUsed = false,
            ValidationAttempts = 0
        };

        _otpStore[referenceCode] = entry;

        // Record send time
        sendTimes.Add(DateTime.UtcNow);

        // Audit log
        _logger.LogInformation(
            "OTP sent. ReferenceCode={ReferenceCode}, Receipt={Receipt}, Type={Type}, OtpType={OtpType}, ExpiresAt={ExpiresAt}",
            referenceCode, MaskReceipt(receipt), type, otpType, entry.ExpiresAt);

        // Development log (for testing - shows actual OTP)
        _logger.LogDebug("OTP Code for {Receipt}: {Code}", MaskReceipt(receipt), otpCode);

        // TODO: In production, send actual SMS/Email here

        return Task.FromResult(new SendOtpResult(referenceCode, entry.ExpiresAt));
    }

    public Task<OtpValidationResult> ValidateOtp(string referenceCode, string otp, OtpType otpType, CancellationToken cancellationToken)
    {
        if (!_otpStore.TryGetValue(referenceCode, out var entry))
        {
            _logger.LogWarning("OTP validation failed. Reference code not found: {ReferenceCode}", referenceCode);
            return Task.FromResult(new OtpValidationResult(false));
        }

        // Check if expired
        if (entry.ExpiresAt < DateTime.UtcNow)
        {
            _otpStore.TryRemove(referenceCode, out _);
            _logger.LogWarning(
                "OTP validation failed. Reference code expired: {ReferenceCode}, Receipt={Receipt}",
                referenceCode, MaskReceipt(entry.Receipt));
            return Task.FromResult(new OtpValidationResult(false));
        }

        // Check if already used
        if (entry.IsUsed)
        {
            _logger.LogWarning(
                "OTP validation failed. Reference code already used: {ReferenceCode}, Receipt={Receipt}",
                referenceCode, MaskReceipt(entry.Receipt));
            return Task.FromResult(new OtpValidationResult(false));
        }

        // Check validation attempts limit
        entry.ValidationAttempts++;
        if (entry.ValidationAttempts > _maxValidationAttempts)
        {
            _otpStore.TryRemove(referenceCode, out _);
            _logger.LogWarning(
                "OTP validation failed. Max attempts ({MaxAttempts}) exceeded: {ReferenceCode}, Receipt={Receipt}",
                _maxValidationAttempts, referenceCode, MaskReceipt(entry.Receipt));
            return Task.FromResult(new OtpValidationResult(false));
        }

        // Check OTP type matches
        if (entry.OtpType != otpType)
        {
            _logger.LogWarning(
                "OTP validation failed. Type mismatch: {ReferenceCode}, Expected={Expected}, Actual={Actual}",
                referenceCode, entry.OtpType, otpType);
            return Task.FromResult(new OtpValidationResult(false));
        }

        // Validate OTP code
        if (entry.OtpCode != otp)
        {
            _logger.LogWarning(
                "OTP validation failed. Invalid code: {ReferenceCode}, Receipt={Receipt}, Attempts={Attempts}/{MaxAttempts}",
                referenceCode, MaskReceipt(entry.Receipt), entry.ValidationAttempts, _maxValidationAttempts);
            return Task.FromResult(new OtpValidationResult(false));
        }

        // Mark as used
        entry.IsUsed = true;

        // Audit log - successful validation
        _logger.LogInformation(
            "OTP validated successfully. ReferenceCode={ReferenceCode}, Receipt={Receipt}, Type={Type}",
            referenceCode, MaskReceipt(entry.Receipt), entry.ReceiptType);

        // Return validation result with receipt info
        var result = new OtpValidationResult(true, entry.Receipt, entry.ReceiptType);

        // Remove from store after successful validation
        _otpStore.TryRemove(referenceCode, out _);

        return Task.FromResult(result);
    }

    public Task SendSms(
        string[] phoneNumbers,
        string body,
        string? sender = null,
        CancellationToken cancellationToken = default)
    {
        // In-memory implementation: just log the SMS
        var recipientList = string.Join(", ", phoneNumbers.Select(MaskReceipt));

        _logger.LogInformation(
            "SMS sent (in-memory). Recipients={Recipients}, Sender={Sender}, Body={Body}",
            recipientList, sender ?? "default", body);

        // Development log (for testing - shows full details)
        _logger.LogDebug(
            "SMS Details - Recipients: [{Numbers}], Body: {Body}",
            string.Join(", ", phoneNumbers), body);

        return Task.CompletedTask;
    }

    private static string GenerateOtpCode()
    {
        // Generate 6-digit OTP
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    private void CleanupOldSendHistory()
    {
        var cutoffTime = DateTime.UtcNow - _rateLimitWindow;
        
        foreach (var kvp in _sendHistory)
        {
            kvp.Value.RemoveAll(t => t < cutoffTime);
            
            // Remove empty entries
            if (kvp.Value.Count == 0)
            {
                _sendHistory.TryRemove(kvp.Key, out _);
            }
        }
    }

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

    private class OtpEntry
    {
        public required string OtpCode { get; set; }
        public required string Receipt { get; set; }
        public required OtpReceiptType ReceiptType { get; set; }
        public required OtpType OtpType { get; set; }
        public required DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public int ValidationAttempts { get; set; }
    }
}
