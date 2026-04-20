using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.CreateWallet;

public class CreateWalletCommandHandler : IRequestHandler<CreateWalletCommand, CreateWalletResponse>
{
    private readonly IWalletReadRepository _readRepo;
    private readonly IWalletWriteRepository _writeRepo;

    public CreateWalletCommandHandler(IWalletReadRepository readRepo, IWalletWriteRepository writeRepo)
    {
        _readRepo = readRepo;
        _writeRepo = writeRepo;
    }

    public async Task<CreateWalletResponse> Handle(CreateWalletCommand request, CancellationToken cancellationToken)
    {
        var walletTypeEnum = MapWalletType(request.WalletType);
        var walletTypeShort = (short)walletTypeEnum;

        // Idempotent: if wallet already exists for this owner+type, return it
        var existing = await _readRepo.ExistsByOwnerAndTypeAsync(request.OwnerId, walletTypeShort, cancellationToken);
        if (existing)
        {
            var owned = await _readRepo.GetByOwnerIdAsync(request.OwnerId, cancellationToken);
            var match = owned.First(w => w.WalletType == request.WalletType);
            return new CreateWalletResponse(match.WalletId, match.WalletType, match.Currency);
        }

        var walletId = await _writeRepo.CreateAsync(
            ownerId: request.OwnerId,
            walletType: walletTypeShort,
            walletStatus: (short)WalletStatus.Active,
            currency: request.Currency.ToUpperInvariant(),
            ct: cancellationToken);

        return new CreateWalletResponse(walletId, request.WalletType.ToUpperInvariant(), request.Currency.ToUpperInvariant());
    }

    private static WalletType MapWalletType(string walletType) =>
        walletType.ToUpperInvariant() switch
        {
            "REFAHI" => WalletType.User,
            _ => throw new ArgumentException($"نوع کیف‌پول '{walletType}' پشتیبانی نمی‌شود", nameof(walletType))
        };
}
