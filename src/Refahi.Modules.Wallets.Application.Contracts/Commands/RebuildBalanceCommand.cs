using System;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Responses;

namespace Refahi.Modules.Wallets.Application.Contracts.Commands;

/// <summary>
/// Admin command to rebuild balance for a single wallet from ledger.
/// </summary>
public record RebuildBalanceCommand(
    Guid WalletId
) : IRequest<CommandResponse<RebuildBalanceResponse>>;
