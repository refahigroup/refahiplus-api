using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Api;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Config;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Contract;

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
public class JibitPaymentGatewayProvider : IRecoverablePaymentGatewayProvider
{
    private readonly JibitApiClient _apiClient;
    private readonly JibitOptions _options;
    private readonly ILogger<JibitPaymentGatewayProvider> _logger;

    public JibitPaymentGatewayProvider(
        JibitApiClient apiClient,
        IOptions<JibitOptions> options,
        ILogger<JibitPaymentGatewayProvider> logger
    )
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
    public async Task<GetTokenResult> GetTokenAsync(
        GetTokenRequest request,
        CancellationToken ct = default
    )
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
                UserIdentifier = string.IsNullOrEmpty(request.CellNumber)
                    ? request.ResNum
                    : request.CellNumber,
                Description = "شارژ کیف‌پول",
            };

            _logger.LogInformation(
                "Jibit: Creating purchase. ClientRef={ClientRef} Amount={Amount}",
                request.ResNum,
                request.AmountMinor
            );

            var response = await _apiClient.CreatePurchaseAsync(jibitRequest, ct);

            if (response.Errors?.Count > 0)
            {
                var firstError = response.Errors.First();
                _logger.LogWarning(
                    "Jibit: CreatePurchase returned errors. Code={Code} Message={Message}",
                    firstError.Code,
                    firstError.Message
                );
                return new GetTokenResult(false, null, ErrorMessage: $"خطای جیبیت: {firstError.Message}");
            }

            if (
                string.IsNullOrEmpty(response.PspSwitchingUrl)
                || string.IsNullOrEmpty(response.PurchaseId)
            )
            {
                _logger.LogWarning(
                    "Jibit: CreatePurchase succeeded but pspSwitchingUrl or purchaseId is empty."
                );
                return new GetTokenResult(false, null, ErrorMessage: "آدرس صفحه پرداخت جیبیت دریافت نشد.");
            }

            _logger.LogInformation(
                "Jibit: Purchase created. PurchaseId={PurchaseId}",
                response.PurchaseId
            );

            return new GetTokenResult(
                true,
                response.PspSwitchingUrl,
                ProviderAttemptId: response.PurchaseId,
                ProviderTransactionId: response.PurchaseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jibit: Exception during CreatePurchase.");
            return new GetTokenResult(
                false, null, ErrorMessage: ex.Message, IsOutcomeUnknown: true);
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
    public async Task<VerifyResult> VerifyAsync(
        VerifyRequest request,
        CancellationToken ct = default
    )
    {
        try
        {
            _logger.LogInformation(
                "Jibit: Verifying purchase. PurchaseId={PurchaseId}",
                request.ProviderTransactionId
            );

            var response = await _apiClient.VerifyPurchaseAsync(request.ProviderTransactionId, ct);

            if (response.Status is not null &&
                (response.Status.Equals("SUCCESSFUL", StringComparison.OrdinalIgnoreCase)
                    || response.Status.Equals("ALREADY_VERIFIED", StringComparison.OrdinalIgnoreCase)))
            {
                return await ReadAuthoritativePurchaseAsync(request, ct);
            }

            var errorCode = response.Errors?.FirstOrDefault()?.Code ?? response.Status ?? "UNKNOWN";
            var description = GetJibitStatusDescription(response.Status, errorCode);

            _logger.LogWarning(
                "Jibit: Verification failed. PurchaseId={PurchaseId} Status={Status}",
                request.ProviderTransactionId,
                response.Status
            );

            var outcome = response.Status?.ToUpperInvariant() switch
            {
                "FAILED" or "PURCHASE_NOT_FOUND" or "EXPIRED" =>
                    FundingProviderOutcome.DefinitiveFailure,
                "UNKNOWN" => FundingProviderOutcome.Unknown,
                _ => FundingProviderOutcome.Pending,
            };
            return new VerifyResult(outcome, request.ProviderTransactionId, null, null, null,
                GetJibitStatusCode(response.Status), description);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Jibit: Exception during VerifyPurchase. PurchaseId={PurchaseId}",
                request.ProviderTransactionId
            );
            return new VerifyResult(FundingProviderOutcome.Unknown,
                request.ProviderTransactionId, null, null, null, -99, ex.Message);
        }
    }

    public async Task<VerifyResult> InquireAsync(
        FundingInquiryRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var detail = await _apiClient.FindPurchaseAsync(
                request.ProviderTransactionId, request.SessionReference, ct);
            var expectedPurchaseId = request.ProviderTransactionId
                ?? detail?.PurchaseIdStr ?? detail?.PurchaseId?.ToString() ?? string.Empty;
            return MapPurchaseDetail(
                detail,
                expectedPurchaseId,
                request.SessionReference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Jibit: Purchase inquiry failed. PurchaseId={PurchaseId}",
                request.ProviderTransactionId);
            return new VerifyResult(FundingProviderOutcome.Unknown,
                request.ProviderTransactionId, null, null, null, -99, ex.Message);
        }
    }

    private async Task<VerifyResult> ReadAuthoritativePurchaseAsync(
        VerifyRequest request,
        CancellationToken ct)
    {
        var detail = await _apiClient.GetPurchaseAsync(request.ProviderTransactionId, ct);
        return MapPurchaseDetail(detail, request.ProviderTransactionId, request.SessionReference);
    }

    private static VerifyResult MapPurchaseDetail(
        JibitPurchaseDetail? detail,
        string expectedPurchaseId,
        string expectedClientReference)
    {
        if (detail is null)
            return new VerifyResult(FundingProviderOutcome.Unknown, expectedPurchaseId,
                null, null, null, -99, "جزئیات authoritative خرید جیبیت در دسترس نیست.");

        var actualId = detail.PurchaseIdStr ?? detail.PurchaseId?.ToString();
        var state = detail.State?.ToUpperInvariant();
        var outcome = state switch
        {
            "SUCCESS" or "SUCCESSFUL" or "MANUALLY_SUCCESS" =>
                FundingProviderOutcome.VerifiedSuccess,
            "FAILED" or "EXPIRED" or "REVERSED" =>
                FundingProviderOutcome.DefinitiveFailure,
            "READY_TO_VERIFY" or "CREATED" or "IN_PROGRESS" =>
                FundingProviderOutcome.Pending,
            _ => FundingProviderOutcome.Unknown,
        };

        if (!string.Equals(actualId, expectedPurchaseId, StringComparison.Ordinal)
            || !string.Equals(detail.ClientReferenceNumber, expectedClientReference,
                StringComparison.Ordinal))
        {
            outcome = FundingProviderOutcome.DefinitiveFailure;
        }

        return new VerifyResult(outcome, actualId, detail.Amount, detail.Currency,
            detail.ClientReferenceNumber, outcome == FundingProviderOutcome.VerifiedSuccess ? 0 : -1,
            outcome == FundingProviderOutcome.VerifiedSuccess ? null : $"Jibit purchase state: {state ?? "UNKNOWN"}.");
    }

    private static int GetJibitStatusCode(string? status) =>
        status switch
        {
            "SUCCESSFUL" => 0,
            "FAILED" => -1,
            "PURCHASE_NOT_FOUND" => -2,
            "ALREADY_VERIFIED" => -3,
            "EXPIRED" => -4,
            _ => -99,
        };

    private static string GetJibitStatusDescription(string? status, string errorCode) =>
        status switch
        {
            "SUCCESSFUL" => "تراکنش موفق",
            "FAILED" => "تراکنش ناموفق",
            "PURCHASE_NOT_FOUND" => "تراکنش یافت نشد",
            "ALREADY_VERIFIED" => "تراکنش قبلاً تأیید شده",
            "EXPIRED" => "مهلت تراکنش منقضی شده",
            _ => $"وضعیت نامشخص تراکنش جیبیت: {errorCode}",
        };
}
