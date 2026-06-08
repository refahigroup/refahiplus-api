using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Providers;

public interface IReversiblePaymentGatewayProvider : IPaymentGatewayProvider
{
    Task<ReverseResult> ReverseAsync(ReverseRequest request, CancellationToken ct = default);
}
