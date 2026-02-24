using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Contracts.Usecases;

public interface IWalletTopUpUsecase
{
    Task<CommandResponse<TopUpWalletResponse>> TopUpAsync(TopUpWalletCommand command, CancellationToken ct);
}
