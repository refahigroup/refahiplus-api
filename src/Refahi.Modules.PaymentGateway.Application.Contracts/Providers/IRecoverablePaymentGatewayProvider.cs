using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Providers;

public interface IRecoverablePaymentGatewayProvider : IPaymentGatewayProvider
{
    Task<VerifyResult> InquireAsync(
        FundingInquiryRequest request,
        CancellationToken ct = default);
}
