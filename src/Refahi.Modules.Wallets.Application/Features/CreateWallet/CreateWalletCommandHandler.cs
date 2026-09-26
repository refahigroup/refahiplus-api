using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Domain.Enums;
using Refahi.Shared.Monetary;

namespace Refahi.Modules.Wallets.Application.Features.CreateWallet;

public class CreateWalletCommandHandler : IRequestHandler<CreateWalletCommand, CreateWalletResponse>
{
    private readonly IWalletReadRepository _readRepo;
    private readonly IWalletWriteRepository _writeRepo;

    public CreateWalletCommandHandler(
        IWalletReadRepository readRepo,
        IWalletWriteRepository writeRepo
    )
    {
        _readRepo = readRepo;
        _writeRepo = writeRepo;
    }

    public async Task<CreateWalletResponse> Handle(
        CreateWalletCommand request,
        CancellationToken cancellationToken
    )
    {
        var currency = CurrencyCode.Parse(request.Currency).Value;
        var walletTypeEnum = MapWalletType(request.WalletType);
        var walletTypeShort = (short)walletTypeEnum;

        // Idempotent: if wallet already exists for this owner+type, return it
        var owned = await _readRepo.GetByOwnerIdAsync(request.OwnerId, cancellationToken);
        var match = owned.FirstOrDefault(w =>
            string.Equals(w.WalletType, walletTypeEnum == WalletType.User ? WalletTypeCodes.Refahi : walletTypeEnum.ToString(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(w.Currency, currency, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return new CreateWalletResponse(match.WalletId, match.WalletType, match.Currency);
        }

        Guid walletId;
        try
        {
            walletId = await _writeRepo.CreateAsync(
                ownerId: request.OwnerId,
                walletType: walletTypeShort,
                walletStatus: (short)WalletStatus.Active,
                currency: currency,
                ct: cancellationToken
            );
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A concurrent create may have committed after the initial read.
            var concurrent = (await _readRepo.GetByOwnerIdAsync(request.OwnerId, cancellationToken))
                .FirstOrDefault(w => string.Equals(w.WalletType,
                    walletTypeEnum == WalletType.User ? WalletTypeCodes.Refahi : walletTypeEnum.ToString(),
                    StringComparison.OrdinalIgnoreCase) && string.Equals(w.Currency, currency, StringComparison.OrdinalIgnoreCase));
            if (concurrent is null) throw;
            return new CreateWalletResponse(concurrent.WalletId, concurrent.WalletType, concurrent.Currency);
        }

        return new CreateWalletResponse(walletId, request.WalletType.ToUpperInvariant(), currency);
    }

    private static WalletType MapWalletType(string walletType) =>
        walletType.ToUpperInvariant() switch
        {
            WalletTypeCodes.Refahi => WalletType.User,
            WalletTypeCodes.Provider => WalletType.Provider,
            WalletTypeCodes.Merchant => WalletType.Merchant,
            _ => throw new ArgumentException(
                $"نوع کیف‌پول '{walletType}' پشتیبانی نمی‌شود",
                nameof(walletType)
            ),
        };
}
