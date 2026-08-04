using System;
using MediatR;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;

public sealed record GetWalletInfoByIdQuery(Guid WalletId) : IRequest<WalletInfoDto?>;
