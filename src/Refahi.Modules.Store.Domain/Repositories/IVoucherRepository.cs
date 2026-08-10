using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Domain.Repositories;

public sealed record VoucherRedemptionHistoryRow(
    Guid VoucherId, Guid StoreOrderId, int SequenceNumber,
    Guid ProductId, string ProductTitle, Guid SupplierId,
    Guid ShopId, string ShopName, Guid RedeemedByUserId, DateTimeOffset RedeemedAtUtc);

public sealed record VoucherRedemptionHistoryPage(
    int Total, IReadOnlyList<VoucherRedemptionHistoryRow> Items);

public interface IVoucherRepository
{
    Task<Voucher?> GetByItemSequenceAsync(Guid storeOrderItemId, int sequenceNumber, CancellationToken ct = default);
    Task<Voucher?> GetByCodeHashAsync(string codeHash, CancellationToken ct = default);
    Task<Voucher?> GetByIdAsync(Guid voucherId, CancellationToken ct = default);
    Task<IReadOnlyList<Voucher>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Voucher>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Voucher>> GetByStoreOrderAsync(Guid storeOrderId, CancellationToken ct = default);
    Task<VoucherRedemption?> GetRedemptionByIdempotencyAsync(Guid vendorUserId, string idempotencyKey, CancellationToken ct = default);
    Task<VoucherRedemptionHistoryPage> GetRedemptionHistoryAsync(
        Guid supplierId, Guid? shopId, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Voucher voucher, CancellationToken ct = default);
    Task RedeemAsync(Voucher voucher, VoucherRedemption redemption, CancellationToken ct = default);
    Task UpdateAsync(Voucher voucher, CancellationToken ct = default);
    Task UpdateRangeAsync(IReadOnlyCollection<Voucher> vouchers, CancellationToken ct = default);
}
