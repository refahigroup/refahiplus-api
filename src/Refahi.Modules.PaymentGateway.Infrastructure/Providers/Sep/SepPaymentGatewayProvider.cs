using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Api;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Config;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Contract;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep;

/// <summary>
/// SEP (Saman Electronic Payment) implementation of IPaymentGatewayProvider.
///
/// Flow:
///   GetTokenAsync → SEP Token endpoint → returns Token
///   BuildRedirectUrl → SEP payment page URL with token
///   VerifyAsync → SEP Verify endpoint → confirms transaction
/// </summary>
public class SepPaymentGatewayProvider : IPaymentGatewayProvider
{
    private readonly SepApiClient _apiClient;
    private readonly SepOptions _options;

    public SepPaymentGatewayProvider(SepApiClient apiClient, IOptions<SepOptions> options)
    {
        _apiClient = apiClient;
        _options = options.Value;
    }

    public PaymentGatewayProviderType ProviderType => PaymentGatewayProviderType.Sep;

    public async Task<GetTokenResult> GetTokenAsync(GetTokenRequest request, CancellationToken ct = default)
    {
        try
        {
            var sepRequest = new SepTokenRequest
            {
                Action = "Token",
                TerminalId = _options.TerminalId,
                Amount = request.AmountMinor,
                ResNum = request.ResNum,
                RedirectURL = request.CallbackUrl,
                CellNumber = request.CellNumber
            };

            var response = await _apiClient.RequestTokenAsync(sepRequest, ct);

            // SEP: Status=1 means success
            if (response.Status == 1 && !string.IsNullOrEmpty(response.Token))
                return new GetTokenResult(true, response.Token);

            return new GetTokenResult(false, null,
                $"SEP token request failed. Status={response.Status} Error={response.ErrorDesc}");
        }
        catch (Exception ex)
        {
            return new GetTokenResult(false, null, ex.Message);
        }
    }

    public string BuildRedirectUrl(string token)
    {
        return $"{_options.PaymentBaseUrl.TrimEnd('/')}?Token={Uri.EscapeDataString(token)}";
    }

    public async Task<VerifyResult> VerifyAsync(VerifyRequest request, CancellationToken ct = default)
    {
        try
        {
            var sepRequest = new SepVerifyRequest
            {
                RefNum = request.RefNum,
                TerminalNumber = long.Parse(_options.TerminalId)
            };

            var response = await _apiClient.VerifyTransactionAsync(sepRequest, ct);

            // SEP: ResultCode=0 means success
            if (response.ResultCode == 0)
            {
                return new VerifyResult(true, response.Amount, response.ResultCode);
            }

            return new VerifyResult(
                false,
                response.Amount,
                response.ResultCode,
                GetSepResultDescription(response.ResultCode));
        }
        catch (Exception ex)
        {
            return new VerifyResult(false, 0, -99, ex.Message);
        }
    }

    /// <summary>
    /// Translates SEP result codes to Persian descriptions.
    /// Based on SEP documentation and sample code error codes.
    /// </summary>
    private static string GetSepResultDescription(int resultCode) => resultCode switch
    {
        0 => "تراکنش موفق",
        -1 => "خطا در بررسی صحت رسید دیجیتالی",
        -2 => "عدم تطابق حساب‌ها هنگام تأیید رسید",
        -3 => "ورودی نامعتبر",
        -4 => "رمز یا حساب نامعتبر",
        -5 => "خطای پایگاه داده",
        -6 => "تراکنش قبلاً برگشت خورده",
        -7 => "رشته ورودی null است",
        -8 => "رشته ورودی خیلی طولانی است",
        -9 => "رشته ورودی غیرمجاز",
        -10 => "رشته ورودی base64 نامعتبر",
        -11 => "رشته ورودی خیلی کوتاه است",
        -14 => "تراکنش نامعتبر",
        -15 => "مبلغ برگشتی اشتباه است",
        -16 => "خطای داخلی سیستم",
        -17 => "تراکنش برگشتی از بانک دیگر",
        -18 => "IP نامعتبر",
        _ => $"خطای ناشناخته (کد {resultCode})"
    };
}
