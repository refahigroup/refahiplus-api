using System;
using MediatR;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;

public sealed record CreateWalletCommand(
    Guid OwnerId,
    string WalletType,
    string Currency
) : IRequest<CreateWalletResponse>;

public sealed record CreateWalletResponse(Guid WalletId, string WalletType, string Currency);
