using Refahi.Modules.Wallets.Application.Contracts.Features.GetBalance;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Contracts.Repositories;

public interface IWalletReadRepository
{
    Task<WalletBalanceResponse> GetWalletBalanceAsync(Guid walletId);
    Task<IReadOnlyList<GetTransactionsResponse>> GetWalletTransactionsAsync(Guid walletId, int take);
    Task<IReadOnlyList<MyWalletTransactionDto>> GetOwnerWalletTransactionsAsync(
        Guid ownerId,
        int take,
        string? walletType = null,
        short? operationType = null,
        short? entryType = null,
        CancellationToken ct = default);
    Task<List<WalletSummaryDto>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);
    Task<bool> ExistsByOwnerAndTypeAsync(Guid ownerId, short walletType, CancellationToken ct = default);
    Task<WalletInfoDto?> GetByIdAsync(Guid walletId, CancellationToken ct = default);
}
