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
/// </summary>
public class SepPaymentGatewayProvider : IReversiblePaymentGatewayProvider
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

            if (response.Status == 1 && !string.IsNullOrEmpty(response.Token))
                return new GetTokenResult(true, response.Token);

            return new GetTokenResult(false, null,
                $"SEP token request failed. Status={response.Status} ErrorCode={response.ErrorCode} Error={response.ErrorDesc}");
        }
        catch (Exception ex)
        {
            return new GetTokenResult(false, null, ex.Message);
        }
    }

    public string BuildRedirectUrl(string token)
    {
        return $"{_options.PaymentBaseUrl.TrimEnd('/')}?token={Uri.EscapeDataString(token)}";
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
            var verifiedAmount = response.TransactionDetail?.OrginalAmount ?? 0;

            if (response.Success &&
                response.ResultCode == 0 &&
                response.TransactionDetail is not null &&
                verifiedAmount == request.ExpectedAmountMinor)
            {
                return new VerifyResult(true, verifiedAmount, response.ResultCode);
            }

            var description = BuildVerifyFailureDescription(response, request.ExpectedAmountMinor, verifiedAmount);

            return new VerifyResult(
                false,
                verifiedAmount,
                response.ResultCode,
                description);
        }
        catch (Exception ex)
        {
            return new VerifyResult(false, 0, -99, ex.Message);
        }
    }

    public async Task<ReverseResult> ReverseAsync(ReverseRequest request, CancellationToken ct = default)
    {
        try
        {
            var sepRequest = new SepReverseRequest
            {
                RefNum = request.RefNum,
                TerminalNumber = long.Parse(_options.TerminalId)
            };

            var response = await _apiClient.ReverseTransactionAsync(sepRequest, ct);

            if (response.Success && response.ResultCode == 0)
                return new ReverseResult(true, response.ResultCode);

            return new ReverseResult(
                false,
                response.ResultCode,
                response.ResultDescription ?? GetSepResultDescription(response.ResultCode));
        }
        catch (Exception ex)
        {
            return new ReverseResult(false, -99, ex.Message);
        }
    }

    private static string BuildVerifyFailureDescription(
        SepVerifyResponse response,
        long expectedAmount,
        long verifiedAmount)
    {
        if (response.ResultCode == 0 && response.TransactionDetail is null)
            return "SEP verify response did not include TransactionDetail.";

        if (response.ResultCode == 0 && verifiedAmount != expectedAmount)
            return $"SEP verified amount mismatch. Expected={expectedAmount} Actual={verifiedAmount}.";

        return response.ResultDescription ?? GetSepResultDescription(response.ResultCode);
    }

    private static string GetSepResultDescription(int resultCode) => resultCode switch
    {
        0 => "SEP transaction succeeded.",
        2 => "SEP duplicate request.",
        5 => "SEP transaction has been reversed.",
        -2 => "SEP transaction was not found.",
        -6 => "SEP transaction verify window has expired.",
        -104 => "SEP terminal is inactive.",
        -105 => "SEP terminal was not found.",
        -106 => "SEP requester IP address is not allowed.",
        _ => $"Unknown SEP result code ({resultCode})."
    };
}
