using System;
using MediatR;

namespace Wallets.Application.Contracts.Features.GetBalance;

/// <summary>
/// Query: Get current materialized balance for a wallet.
/// </summary>
public sealed record GetWalletBalanceQuery(Guid WalletId) : IRequest<WalletBalanceResponse?>;
