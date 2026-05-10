using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Api;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Config;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Contract;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit;

/// <summary>
/// Jibit PPG v3 implementation of IPaymentGatewayProvider.
///
/// Flow:
///   GetTokenAsync → POST /purchases → returns pspSwitchingUrl as "token"
///   BuildRedirectUrl → returns the pspSwitchingUrl directly
///   VerifyAsync → GET /purchases/{purchaseId}/verify → confirms SUCCESSFUL
///
/// Callback URL convention:
///   The Jibit provider appends "/{sessionId}" to the callbackUrl so the
///   backend can identify the session without a DB lookup by purchaseId.
///   e.g. https://api.refahi.xyz/api/payment-gateway/callback/jibit/{sessionId}
/// </summary>
public class JibitPaymentGatewayProvider : IPaymentGatewayProvider
{
    private readonly JibitApiClient _apiClient;
    private readonly JibitOptions _options;
    private readonly ILogger<JibitPaymentGatewayProvider> _logger;

    public JibitPaymentGatewayProvider(
        JibitApiClient apiClient,
        IOptions<JibitOptions> options,
        ILogger<JibitPaymentGatewayProvider> logger)
    {
        _apiClient = apiClient;
        _options = options.Value;
        _logger = logger;
    }

    public PaymentGatewayProviderType ProviderType => PaymentGatewayProviderType.Jibit;

    /// <summary>
    /// ایجاد درخواست پرداخت در جیبیت.
    /// Token بازگشتی = pspSwitchingUrl (آدرس مستقیم صفحه پرداخت).
    /// </summary>
    public async Task<GetTokenResult> GetTokenAsync(GetTokenRequest request, CancellationToken ct = default)
    {
        try
        {
            // Append sessionId to callbackUrl so the Jibit callback endpoint can identify the session
            var callbackUrl = $"{request.CallbackUrl.TrimEnd('/')}/{request.ResNum}";

            var jibitRequest = new JibitCreatePurchaseRequest
            {
                Amount = request.AmountMinor,
                CallbackUrl = callbackUrl,
                ClientReferenceNumber = request.ResNum,
                Currency = "IRR",
                UserIdentifier = string.IsNullOrEmpty(request.CellNumber) ? request.ResNum : request.CellNumber,
                Description = "شارژ کیف‌پول"
            };

            _logger.LogInformation(
                "Jibit: Creating purchase. ClientRef={ClientRef} Amount={Amount}",
                request.ResNum, request.AmountMinor);

            var response = await _apiClient.CreatePurchaseAsync(jibitRequest, ct);

            if (response.Errors?.Count > 0)
            {
                var firstError = response.Errors.First();
                _logger.LogWarning(
                    "Jibit: CreatePurchase returned errors. Code={Code} Message={Message}",
                    firstError.Code, firstError.Message);
                return new GetTokenResult(false, null, $"خطای جیبیت: {firstError.Message}");
            }

            if (string.IsNullOrEmpty(response.PspSwitchingUrl) || string.IsNullOrEmpty(response.PurchaseId))
            {
                _logger.LogWarning("Jibit: CreatePurchase succeeded but pspSwitchingUrl or purchaseId is empty.");
                return new GetTokenResult(false, null, "آدرس صفحه پرداخت جیبیت دریافت نشد.");
            }

            _logger.LogInformation(
                "Jibit: Purchase created. PurchaseId={PurchaseId}", response.PurchaseId);

            // We store only the pspSwitchingUrl as the "token".
            // The purchaseId comes back from Jibit's callback directly.
            return new GetTokenResult(true, response.PspSwitchingUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jibit: Exception during CreatePurchase.");
            return new GetTokenResult(false, null, ex.Message);
        }
    }

    /// <summary>
    /// The "token" for Jibit IS the direct redirect URL (pspSwitchingUrl).
    /// </summary>
    public string BuildRedirectUrl(string token) => token;

    /// <summary>
    /// تأیید تراکنش جیبیت.
    /// request.RefNum = purchaseId دریافت شده از callback جیبیت.
    /// </summary>
    public async Task<VerifyResult> VerifyAsync(VerifyRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Jibit: Verifying purchase. PurchaseId={PurchaseId}", request.RefNum);

            var response = await _apiClient.VerifyPurchaseAsync(request.RefNum, ct);

            if (response.Status?.Equals("SUCCESSFUL", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogInformation("Jibit: Purchase verified successfully. PurchaseId={PurchaseId}", request.RefNum);
                return new VerifyResult(true, request.ExpectedAmountMinor, 0);
            }

            var errorCode = response.Errors?.FirstOrDefault()?.Code ?? response.Status ?? "UNKNOWN";
            var description = GetJibitStatusDescription(response.Status, errorCode);

            _logger.LogWarning(
                "Jibit: Verification failed. PurchaseId={PurchaseId} Status={Status}",
                request.RefNum, response.Status);

            return new VerifyResult(false, 0, GetJibitStatusCode(response.Status), description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jibit: Exception during VerifyPurchase. PurchaseId={PurchaseId}", request.RefNum);
            return new VerifyResult(false, 0, -99, ex.Message);
        }
    }

    private static int GetJibitStatusCode(string? status) => status switch
    {
        "SUCCESSFUL" => 0,
        "FAILED" => -1,
        "PURCHASE_NOT_FOUND" => -2,
        "ALREADY_VERIFIED" => -3,
        "EXPIRED" => -4,
        _ => -99
    };

    private static string GetJibitStatusDescription(string? status, string errorCode) => status switch
    {
        "SUCCESSFUL" => "تراکنش موفق",
        "FAILED" => "تراکنش ناموفق",
        "PURCHASE_NOT_FOUND" => "تراکنش یافت نشد",
        "ALREADY_VERIFIED" => "تراکنش قبلاً تأیید شده",
        "EXPIRED" => "مهلت تراکنش منقضی شده",
        _ => $"وضعیت نامشخص تراکنش جیبیت: {errorCode}"
    };
}
