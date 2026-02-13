using System;
using System.Collections.Generic;
using MediatR;

namespace Wallets.Application.Contracts.Features.GetTransactions;

/// <summary>
/// Query: Get recent ledger postings for a wallet.
/// </summary>
public sealed record GetWalletTransactionsQuery(Guid WalletId, int Take = 20) : IRequest<IReadOnlyList<GetTransactionsResponse>?>;
