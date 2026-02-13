using System.Threading;
using System.Threading.Tasks;
using Wallets.Application.Contracts.Features.TopUp;

namespace Wallets.Application.Contracts.Usecases;

public interface IWalletTopUpUsecase
{
    Task<CommandResponse<TopUpWalletResponse>> TopUpAsync(TopUpWalletCommand command, CancellationToken ct);
}
