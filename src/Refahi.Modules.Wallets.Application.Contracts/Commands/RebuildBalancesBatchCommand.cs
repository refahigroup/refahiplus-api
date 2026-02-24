using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Responses;

namespace Refahi.Modules.Wallets.Application.Contracts.Commands;

/// <summary>
/// Admin command to rebuild balances for multiple wallets (batch operation).
/// </summary>
public record RebuildBalancesBatchCommand(
    string? Currency = null,
    bool OnlyActive = true
) : IRequest<CommandResponse<BatchRebuildResponse>>;
