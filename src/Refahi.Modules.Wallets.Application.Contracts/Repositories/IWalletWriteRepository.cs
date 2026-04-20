using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Contracts.Repositories;

public interface IWalletWriteRepository
{
    /// <summary>
    /// Inserts a new wallet. WalletType and Status are persisted as their short values.
    /// </summary>
    Task<Guid> CreateAsync(Guid ownerId, short walletType, short walletStatus, string currency, CancellationToken ct = default);

    /// <summary>
    /// Inserts a new OrgCredit wallet with optional category restriction and contract expiry.
    /// </summary>
    Task<Guid> CreateOrgCreditAsync(Guid ownerId, string currency, string? allowedCategoryCode, DateTimeOffset? contractExpiresAt, CancellationToken ct = default);
}
