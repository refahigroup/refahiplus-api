using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.GetMyWallets;

public class GetMyWalletsQueryHandler : IRequestHandler<GetMyWalletsQuery, List<WalletSummaryDto>>
{
    private readonly IWalletReadRepository _repo;

    public GetMyWalletsQueryHandler(IWalletReadRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<WalletSummaryDto>> Handle(GetMyWalletsQuery request, CancellationToken cancellationToken)
    {
        return await _repo.GetByOwnerIdAsync(request.UserId, cancellationToken);
    }
}
