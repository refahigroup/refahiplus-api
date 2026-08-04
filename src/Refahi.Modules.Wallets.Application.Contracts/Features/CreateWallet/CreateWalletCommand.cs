using System;
using MediatR;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;

public static class WalletTypeCodes
{
    public const string Refahi = "REFAHI";
    public const string Provider = "PROVIDER";
}

public sealed record CreateWalletCommand(
    Guid OwnerId,
    string WalletType,
    string Currency
) : IRequest<CreateWalletResponse>;

public sealed record CreateWalletResponse(Guid WalletId, string WalletType, string Currency);
