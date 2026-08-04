using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;

namespace Refahi.Modules.Wallets.Application.Features.GetWalletInfo;

public sealed class GetWalletInfoByIdQueryHandler(IWalletReadRepository repository)
    : IRequestHandler<GetWalletInfoByIdQuery, WalletInfoDto?>
{
    public Task<WalletInfoDto?> Handle(GetWalletInfoByIdQuery request, CancellationToken cancellationToken)
        => repository.GetByIdAsync(request.WalletId, cancellationToken);
}
