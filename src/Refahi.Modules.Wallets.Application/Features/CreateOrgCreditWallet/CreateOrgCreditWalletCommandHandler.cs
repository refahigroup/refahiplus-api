using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateOrgCreditWallet;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.CreateOrgCreditWallet;

public sealed class CreateOrgCreditWalletCommandHandler : IRequestHandler<CreateOrgCreditWalletCommand, CreateOrgCreditWalletResponse>
{
    private readonly IWalletWriteRepository _writeRepo;

    public CreateOrgCreditWalletCommandHandler(IWalletWriteRepository writeRepo)
    {
        _writeRepo = writeRepo;
    }

    public async Task<CreateOrgCreditWalletResponse> Handle(
        CreateOrgCreditWalletCommand request, CancellationToken cancellationToken)
    {
        var walletId = await _writeRepo.CreateOrgCreditAsync(
            ownerId: request.OwnerId,
            currency: request.Currency,
            allowedCategoryCode: request.AllowedCategoryCode,
            contractExpiresAt: request.ContractExpiresAt,
            ct: cancellationToken);

        return new CreateOrgCreditWalletResponse(
            WalletId: walletId,
            WalletType: "ORG_CREDIT",
            Currency: request.Currency.ToUpperInvariant(),
            AllowedCategoryCode: request.AllowedCategoryCode,
            ContractExpiresAt: request.ContractExpiresAt);
    }
}
