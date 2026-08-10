using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Application.Contracts.Vouchers;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;

namespace Refahi.Modules.Store.Application.Features.Vouchers;

public sealed class IssueVouchersAfterOrderPaidHandler(
    IStoreOrderRepository storeOrders,
    IVoucherRepository vouchers,
    IShopRepository shops,
    IMediator mediator,
    IVoucherCodeProtector protector,
    TimeProvider timeProvider,
    ILogger<IssueVouchersAfterOrderPaidHandler> logger
) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public async Task Handle(OrderPaidIntegrationEvent notification, CancellationToken ct)
    {
        if (
            !notification.SourceModule.Equals("Store", StringComparison.OrdinalIgnoreCase)
            || !notification.ReferenceType.Equals("StoreOrder", StringComparison.OrdinalIgnoreCase)
        )
            return;

        var storeOrder = await storeOrders.GetByOrderIdAsync(notification.OrderId, ct);
        if (
            storeOrder is null
            || storeOrder.OrderId != notification.OrderId
            || storeOrder.UserId != notification.UserId
            || notification.SourceReferenceId != storeOrder.Id
        )
            throw new StoreDomainException(
                "مالکیت سفارش فروشگاه برای صدور ووچر تایید نشد",
                "VOUCHER_ORDER_OWNERSHIP_MISMATCH"
            );

        var voucherItems = storeOrder
            .Items.Where(x => x.FulfillmentMethod == FulfillmentMethod.Voucher)
            .ToArray();
        if (voucherItems.Length == 0)
            return;
        var shop = await shops.GetByIdAsync(storeOrder.ShopId, ct);
        if (shop is null || shop.SupplierId != storeOrder.SupplierId)
            throw new StoreDomainException(
                "فروشگاه سفارش برای صدور ووچر یافت نشد",
                "VOUCHER_SHOP_SNAPSHOT_UNAVAILABLE"
            );
        var supplier = await mediator.Send(new GetSupplierByIdQuery(storeOrder.SupplierId), ct);
        var supplierName =
            supplier?.BrandName
            ?? supplier?.CompanyName
            ?? string.Join(
                ' ',
                new[] { supplier?.FirstName, supplier?.LastName }.Where(x =>
                    !string.IsNullOrWhiteSpace(x)
                )
            );
        if (string.IsNullOrWhiteSpace(supplierName))
            throw new StoreDomainException(
                "تامین‌کننده سفارش برای صدور ووچر یافت نشد",
                "VOUCHER_SUPPLIER_SNAPSHOT_UNAVAILABLE"
            );

        var issued = 0;
        foreach (var item in voucherItems)
        {
            for (var sequence = 1; sequence <= item.Quantity; sequence++)
            {
                if (await vouchers.GetByItemSequenceAsync(item.Id, sequence, ct) is not null)
                    continue;
                for (var collisionAttempt = 0; collisionAttempt < 5; collisionAttempt++)
                {
                    var plaintext = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                    var hash = VoucherCode.Hash(plaintext);
                    var ciphertext = protector.Protect(plaintext);
                    var voucher = Voucher.Issue(
                        storeOrder.Id,
                        item.Id,
                        notification.OrderId,
                        notification.OrderNumber,
                        sequence,
                        storeOrder.UserId,
                        item.SupplierId,
                        supplierName,
                        item.ShopId,
                        shop.Name,
                        item.ProductId,
                        item.ProductTitle,
                        hash,
                        ciphertext,
                        timeProvider.GetUtcNow()
                    );
                    try
                    {
                        await vouchers.AddAsync(voucher, ct);
                        issued++;
                        break;
                    }
                    catch (StoreDomainException ex) when (ex.ErrorCode == "VOUCHER_UNIQUE_CONFLICT")
                    {
                        if (
                            await vouchers.GetByItemSequenceAsync(item.Id, sequence, ct) is not null
                        )
                            break;
                        if (collisionAttempt == 4)
                            throw;
                    }
                }
            }
        }
        logger.LogInformation(
            "Voucher issuance completed. OrderId={OrderId}, StoreOrderId={StoreOrderId}, IssuedCount={IssuedCount}",
            notification.OrderId,
            storeOrder.Id,
            issued
        );
    }
}

public sealed class RedeemVoucherHandler(
    IVoucherRepository vouchers,
    IShopRepository shops,
    IMediator mediator,
    TimeProvider timeProvider
) : IRequestHandler<RedeemVoucherCommand, VoucherRedemptionDto>
{
    public async Task<VoucherRedemptionDto> Handle(
        RedeemVoucherCommand request,
        CancellationToken ct
    )
    {
        var normalizedCode = VoucherCode.Normalize(request.Code);
        var codeHash = VoucherCode.Hash(normalizedCode);
        var fingerprint = VoucherCode.Hash($"{request.ShopId:N}:{codeHash}");
        var cached = await vouchers.GetRedemptionByIdempotencyAsync(
            request.VendorUserId,
            request.IdempotencyKey.Trim(),
            ct
        );
        if (cached is not null)
            return await CachedAsync(cached, fingerprint, ct);

        var voucher = await vouchers.GetByCodeHashAsync(codeHash, ct) ?? throw NotRedeemable();
        var shop = await shops.GetByIdAsync(request.ShopId, ct);
        if (
            shop is null
            || shop.Status != ShopStatus.Active
            || shop.ShopType != ShopType.InPerson
            || shop.SupplierId != voucher.SupplierId
        )
            throw NotRedeemable();
        if (
            !await mediator.Send(
                new AuthorizeStoreResourceQuery(
                    request.VendorUserId,
                    voucher.SupplierId,
                    request.ShopId,
                    StorePermissions.RedeemVoucher
                ),
                ct
            )
        )
            throw new VoucherApplicationException(
                "VOUCHER_REDEEM_FORBIDDEN",
                "دسترسی استفاده از ووچر را ندارید"
            );

        if (voucher.ExpireIfNeeded(timeProvider.GetUtcNow()))
        {
            await vouchers.UpdateAsync(voucher, ct);
            throw NotRedeemable();
        }
        if (voucher.Status == VoucherStatus.Redeemed)
            throw new VoucherApplicationException(
                "VOUCHER_ALREADY_REDEEMED",
                "این ووچر قبلاً استفاده شده است"
            );
        if (voucher.Status != VoucherStatus.Issued)
            throw NotRedeemable();

        var now = timeProvider.GetUtcNow();
        voucher.Redeem(request.VendorUserId, request.ShopId, shop.Name, now);
        var redemption = VoucherRedemption.Create(
            voucher.Id,
            request.VendorUserId,
            voucher.SupplierId,
            request.ShopId,
            request.IdempotencyKey,
            fingerprint,
            now
        );
        try
        {
            await vouchers.RedeemAsync(voucher, redemption, ct);
        }
        catch (StoreDomainException ex) when (ex.ErrorCode == "VOUCHER_IDEMPOTENCY_CONFLICT")
        {
            cached = await vouchers.GetRedemptionByIdempotencyAsync(
                request.VendorUserId,
                request.IdempotencyKey.Trim(),
                ct
            );
            if (cached is not null)
                return await CachedAsync(cached, fingerprint, ct);
            throw;
        }
        catch (StoreConcurrencyException)
        {
            cached = await vouchers.GetRedemptionByIdempotencyAsync(
                request.VendorUserId,
                request.IdempotencyKey.Trim(),
                ct
            );
            if (cached is not null)
                return await CachedAsync(cached, fingerprint, ct);
            throw new VoucherApplicationException(
                "VOUCHER_ALREADY_REDEEMED",
                "این ووچر قبلاً استفاده شده است"
            );
        }
        return Map(voucher, redemption);
    }

    private async Task<VoucherRedemptionDto> CachedAsync(
        VoucherRedemption redemption,
        string fingerprint,
        CancellationToken ct
    )
    {
        if (!string.Equals(redemption.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            throw new VoucherApplicationException(
                "IDEMPOTENCY_PAYLOAD_MISMATCH",
                "کلید یکتایی با اطلاعات متفاوتی استفاده شده است"
            );
        var voucher =
            await vouchers.GetByIdAsync(redemption.VoucherId, ct)
            ?? throw new VoucherApplicationException(
                "VOUCHER_AUDIT_INCONSISTENT",
                "اطلاعات ووچر کامل نیست"
            );
        return Map(voucher, redemption);
    }

    private static VoucherRedemptionDto Map(Voucher voucher, VoucherRedemption redemption) =>
        new(
            voucher.Id,
            voucher.StoreOrderId,
            voucher.ProductId,
            voucher.ProductTitle,
            redemption.VendorUserId,
            redemption.SupplierId,
            redemption.ShopId,
            redemption.RedeemedAtUtc
        );

    private static VoucherApplicationException NotRedeemable() =>
        new("VOUCHER_NOT_REDEEMABLE", "کد ووچر معتبر یا قابل استفاده نیست");
}

public sealed class GetMyVouchersHandler(
    IVoucherRepository vouchers,
    IVoucherCodeProtector protector
)
    : IRequestHandler<GetMyVouchersQuery, IReadOnlyList<VoucherDto>>,
        IRequestHandler<GetMyVoucherQuery, VoucherDto?>
{
    public async Task<IReadOnlyList<VoucherDto>> Handle(
        GetMyVouchersQuery request,
        CancellationToken ct
    )
    {
        var rows = await vouchers.GetByUserAsync(request.UserId, ct);
        var result = new List<VoucherDto>(rows.Count);
        foreach (var row in rows)
            result.Add(MapOwner(row));
        return result;
    }

    public async Task<VoucherDto?> Handle(GetMyVoucherQuery request, CancellationToken ct)
    {
        var voucher = await vouchers.GetByIdAsync(request.VoucherId, ct);
        return voucher is null || voucher.UserId != request.UserId ? null : MapOwner(voucher);
    }

    private VoucherDto MapOwner(Voucher voucher)
    {
        if (
            !protector.TryUnprotect(voucher.CodeCiphertext, out var code)
            || string.IsNullOrWhiteSpace(code)
        )
            throw new VoucherApplicationException(
                "VOUCHER_CODE_UNAVAILABLE",
                "نمایش کد ووچر در حال حاضر امکان‌پذیر نیست"
            );
        return new(
            voucher.Id,
            voucher.StoreOrderId,
            voucher.StoreOrderItemId,
            voucher.SequenceNumber,
            voucher.OrderId,
            voucher.OrderNumber,
            voucher.SupplierId,
            voucher.SupplierName,
            voucher.ShopId,
            voucher.ShopName,
            voucher.ProductId,
            voucher.ProductTitle,
            code,
            voucher.Status.ToString(),
            voucher.IssuedAtUtc,
            voucher.RedeemedAtUtc,
            voucher.RedeemedShopId,
            voucher.RedeemedShopName,
            voucher.RevokedAtUtc,
            voucher.ExpiresAtUtc
        );
    }
}

public sealed class GetVendorVoucherRedemptionHistoryHandler(
    IVoucherRepository vouchers,
    IMediator mediator
) : IRequestHandler<GetVendorVoucherRedemptionHistoryQuery, VoucherRedemptionHistoryPageDto>
{
    public async Task<VoucherRedemptionHistoryPageDto> Handle(
        GetVendorVoucherRedemptionHistoryQuery request,
        CancellationToken ct
    )
    {
        if (
            !await mediator.Send(
                new AuthorizeStoreResourceQuery(
                    request.VendorUserId,
                    request.SupplierId,
                    request.ShopId,
                    StorePermissions.RedeemVoucher
                ),
                ct
            )
        )
            throw new VoucherApplicationException(
                "VOUCHER_HISTORY_FORBIDDEN",
                "دسترسی مشاهده تاریخچه ووچر را ندارید"
            );
        var result = await vouchers.GetRedemptionHistoryAsync(
            request.SupplierId,
            request.ShopId,
            request.Page,
            request.PageSize,
            ct
        );
        return new(
            request.Page,
            request.PageSize,
            result.Total,
            result
                .Items.Select(row => new VoucherRedemptionHistoryItemDto(
                    row.VoucherId,
                    MaskedReference(row.VoucherId, row.SequenceNumber),
                    row.StoreOrderId,
                    row.ProductId,
                    row.ProductTitle,
                    row.SupplierId,
                    row.ShopId,
                    row.ShopName,
                    row.RedeemedByUserId,
                    row.RedeemedAtUtc
                ))
                .ToArray()
        );
    }

    private static string MaskedReference(Guid voucherId, int sequenceNumber) =>
        $"VCH-****-{sequenceNumber:D4}-{voucherId:N}"[..22].ToUpperInvariant();
}

public sealed class GetAdminVoucherAuditHandler(IVoucherRepository vouchers)
    : IRequestHandler<GetAdminVoucherAuditQuery, IReadOnlyList<VoucherAuditDto>>
{
    public async Task<IReadOnlyList<VoucherAuditDto>> Handle(
        GetAdminVoucherAuditQuery request,
        CancellationToken ct
    )
    {
        IReadOnlyList<Voucher> rows;
        if (request.VoucherId.HasValue)
        {
            var row = await vouchers.GetByIdAsync(request.VoucherId.Value, ct);
            rows = row is null ? [] : [row];
        }
        else if (request.StoreOrderId.HasValue)
            rows = await vouchers.GetByStoreOrderAsync(request.StoreOrderId.Value, ct);
        else
            rows = await vouchers.GetAllAsync(ct);

        var result = new List<VoucherAuditDto>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(
                new(
                    row.Id,
                    row.StoreOrderId,
                    row.StoreOrderItemId,
                    row.SequenceNumber,
                    row.OrderId,
                    row.OrderNumber,
                    row.UserId,
                    row.SupplierId,
                    row.SupplierName,
                    row.ShopId,
                    row.ShopName,
                    row.ProductId,
                    row.ProductTitle,
                    row.Status.ToString(),
                    row.IssuedAtUtc,
                    row.RedeemedAtUtc,
                    row.RedeemedByUserId,
                    row.RedeemedShopId,
                    row.RedeemedShopName,
                    row.RevokedAtUtc,
                    row.RevocationReason,
                    row.ExpiresAtUtc
                )
            );
        }
        return result;
    }
}

public sealed class PrepareStoreOrderRefundHandler(
    IStoreOrderRepository storeOrders,
    IVoucherRepository vouchers,
    IVoucherRefundOverrideRepository overrides,
    IStoreOrderMutationLock mutationLock,
    TimeProvider timeProvider
) : IRequestHandler<PrepareStoreOrderRefundCommand, PrepareStoreOrderRefundResponse>
{
    public async Task<PrepareStoreOrderRefundResponse> Handle(
        PrepareStoreOrderRefundCommand request,
        CancellationToken ct
    )
    {
        await using var handle = await mutationLock.AcquireAsync(request.OrderId, ct);
        var order = await storeOrders.GetByOrderIdAsync(request.OrderId, ct);
        if (order is null)
            return new(Guid.Empty, 0);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var rows = await vouchers.GetByStoreOrderAsync(order.Id, ct);
            var redeemed = rows.Where(x => x.Status == VoucherStatus.Redeemed).ToArray();
            if (redeemed.Length > 0)
            {
                if (!request.VoucherRefundOverrideId.HasValue)
                    throw new VoucherApplicationException(
                        "REDEEMED_VOUCHER_REFUND_REQUIRES_OVERRIDE",
                        "به دلیل استفاده شدن ووچر، بازگشت وجه خودکار امکان‌پذیر نیست"
                    );
                var refundOverride = await overrides.GetByIdAsync(
                    request.VoucherRefundOverrideId.Value,
                    ct
                );
                if (
                    refundOverride is null
                    || refundOverride.OrderId != request.OrderId
                    || refundOverride.StoreOrderId != order.Id
                    || !string.Equals(
                        refundOverride.Reason,
                        request.Reason.Trim(),
                        StringComparison.Ordinal
                    )
                )
                    throw new VoucherApplicationException(
                        "VOUCHER_REFUND_OVERRIDE_INVALID",
                        "مجوز استثنای بازگشت وجه معتبر نیست"
                    );
                var captured =
                    JsonSerializer.Deserialize<VoucherRefundSnapshotItemDto[]>(
                        refundOverride.VoucherSnapshotJson
                    ) ?? [];
                var capturedRedeemed = captured
                    .Where(x => x.Status == VoucherStatus.Redeemed.ToString())
                    .Select(x => x.VoucherId)
                    .ToHashSet();
                if (redeemed.Any(x => !capturedRedeemed.Contains(x.Id)))
                    throw new VoucherApplicationException(
                        "VOUCHER_REFUND_OVERRIDE_STALE",
                        "وضعیت ووچر پس از ثبت مجوز تغییر کرده است؛ بررسی مجدد مدیر لازم است"
                    );
            }
            var changed = rows.Where(x => x.Status == VoucherStatus.Issued).ToArray();
            foreach (var voucher in changed)
                voucher.RevokeForRefund(request.Reason, timeProvider.GetUtcNow());
            try
            {
                await vouchers.UpdateRangeAsync(changed, ct);
                return new(order.Id, changed.Length);
            }
            catch (StoreConcurrencyException) when (attempt < 2) { }
        }
        throw new VoucherApplicationException(
            "VOUCHER_REFUND_CONCURRENCY_CONFLICT",
            "وضعیت ووچر هم‌زمان تغییر کرده است؛ دوباره تلاش کنید"
        );
    }
}

public sealed class GetAdminStoreOrderRefundHandler(
    IStoreOrderRepository storeOrders,
    IVoucherRepository vouchers,
    IVoucherRefundOverrideRepository overrides
) : IRequestHandler<GetAdminStoreOrderRefundQuery, AdminStoreOrderRefundDto?>
{
    public async Task<AdminStoreOrderRefundDto?> Handle(
        GetAdminStoreOrderRefundQuery request,
        CancellationToken ct
    )
    {
        var storeOrder = await storeOrders.GetByOrderIdAsync(request.OrderId, ct);
        if (storeOrder is null || storeOrder.OrderId != request.OrderId)
            return null;
        var rows = await vouchers.GetByStoreOrderAsync(storeOrder.Id, ct);
        var refundOverride = await overrides.GetByOrderIdAsync(request.OrderId, ct);
        var overrideDto = refundOverride is null
            ? null
            : await MapOverrideAsync(refundOverride, overrides, ct);
        var blocked =
            rows.Any(x => x.Status == VoucherStatus.Redeemed)
            && storeOrder.Status != StoreOrderStatus.Refunded;
        return new AdminStoreOrderRefundDto(
            storeOrder.Id,
            request.OrderId,
            storeOrder.Status.ToString(),
            storeOrder.FinalAmountMinor,
            blocked,
            blocked ? "REDEEMED_VOUCHER_REFUND_REQUIRES_OVERRIDE" : null,
            rows.Select(MapAudit).ToArray(),
            overrideDto
        );
    }

    internal static VoucherAuditDto MapAudit(Voucher row) =>
        new(
            row.Id,
            row.StoreOrderId,
            row.StoreOrderItemId,
            row.SequenceNumber,
            row.OrderId,
            row.OrderNumber,
            row.UserId,
            row.SupplierId,
            row.SupplierName,
            row.ShopId,
            row.ShopName,
            row.ProductId,
            row.ProductTitle,
            row.Status.ToString(),
            row.IssuedAtUtc,
            row.RedeemedAtUtc,
            row.RedeemedByUserId,
            row.RedeemedShopId,
            row.RedeemedShopName,
            row.RevokedAtUtc,
            row.RevocationReason,
            row.ExpiresAtUtc
        );

    internal static async Task<VoucherRefundOverrideDto> MapOverrideAsync(
        VoucherRefundOverride value,
        IVoucherRefundOverrideRepository repository,
        CancellationToken ct
    )
    {
        var attempts = await repository.GetAttemptsAsync(value.Id, ct);
        var snapshot =
            JsonSerializer.Deserialize<VoucherRefundSnapshotItemDto[]>(value.VoucherSnapshotJson)
            ?? [];
        var mappedAttempts = attempts
            .Select(x => new VoucherRefundOverrideAttemptDto(
                x.SequenceNumber,
                x.Outcome,
                x.PaymentAction,
                x.FailureCode,
                x.FailureMessage,
                x.CreatedAtUtc
            ))
            .ToArray();
        var outcome = attempts.LastOrDefault()?.Outcome ?? value.Outcome;
        return new VoucherRefundOverrideDto(
            value.Id,
            value.StoreOrderId,
            value.OrderId,
            value.AdminUserId,
            value.Reason,
            snapshot,
            value.CreatedAtUtc,
            value.CorrelationId,
            outcome,
            mappedAttempts
        );
    }
}

public sealed class OverrideRedeemedVoucherRefundHandler(
    IStoreOrderRepository storeOrders,
    IVoucherRepository vouchers,
    IVoucherRefundOverrideRepository overrides,
    IStoreOrderMutationLock mutationLock,
    IMediator mediator,
    TimeProvider timeProvider,
    ILogger<OverrideRedeemedVoucherRefundHandler> logger
) : IRequestHandler<OverrideRedeemedVoucherRefundCommand, VoucherRefundOverrideDto>
{
    public async Task<VoucherRefundOverrideDto> Handle(
        OverrideRedeemedVoucherRefundCommand request,
        CancellationToken ct
    )
    {
        var normalizedReason = request.Reason.Trim();
        var key = request.IdempotencyKey.Trim();
        var fingerprint = VoucherCode.Hash(
            $"{request.OrderId:N}:{request.AdminUserId:N}:{normalizedReason}"
        );
        VoucherRefundOverride value;

        await using (await mutationLock.AcquireAsync(request.OrderId, ct))
        {
            var byKey = await overrides.GetByIdempotencyKeyAsync(key, ct);
            if (byKey is not null)
            {
                if (!string.Equals(byKey.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                    throw new VoucherApplicationException(
                        "IDEMPOTENCY_PAYLOAD_MISMATCH",
                        "کلید یکتایی با اطلاعات متفاوتی استفاده شده است"
                    );
                value = byKey;
            }
            else
            {
                var existing = await overrides.GetByOrderIdAsync(request.OrderId, ct);
                if (existing is not null)
                {
                    if (!string.Equals(existing.Reason, normalizedReason, StringComparison.Ordinal))
                        throw new VoucherApplicationException(
                            "VOUCHER_REFUND_OVERRIDE_CONFLICT",
                            "برای این سفارش قبلاً مجوزی با دلیل دیگری ثبت شده است"
                        );
                    value = existing;
                }
                else
                {
                    var storeOrder =
                        await storeOrders.GetByOrderIdAsync(request.OrderId, ct)
                        ?? throw new VoucherApplicationException(
                            "STORE_ORDER_NOT_FOUND",
                            "سفارش فروشگاه یافت نشد"
                        );
                    if (storeOrder.OrderId != request.OrderId)
                        throw new VoucherApplicationException(
                            "STORE_ORDER_OWNERSHIP_MISMATCH",
                            "ارتباط سفارش فروشگاه معتبر نیست"
                        );
                    if (storeOrder.Status != StoreOrderStatus.Paid)
                        throw new VoucherApplicationException(
                            "STORE_ORDER_NOT_REFUNDABLE",
                            "سفارش فروشگاه در وضعیت قابل بازگشت وجه نیست"
                        );
                    var rows = await vouchers.GetByStoreOrderAsync(storeOrder.Id, ct);
                    if (!rows.Any(x => x.Status == VoucherStatus.Redeemed))
                        throw new VoucherApplicationException(
                            "VOUCHER_REFUND_OVERRIDE_NOT_REQUIRED",
                            "این سفارش ووچر استفاده‌شده ندارد و باید از مسیر عادی بازگشت وجه استفاده شود"
                        );
                    var snapshot = JsonSerializer.Serialize(
                        rows.Select(x => new VoucherRefundSnapshotItemDto(
                            x.Id,
                            x.Status.ToString()
                        ))
                    );
                    value = VoucherRefundOverride.Create(
                        storeOrder.Id,
                        request.OrderId,
                        request.AdminUserId,
                        normalizedReason,
                        snapshot,
                        key,
                        fingerprint,
                        Guid.NewGuid(),
                        timeProvider.GetUtcNow()
                    );
                    await overrides.AddAsync(value, ct);
                    logger.LogWarning(
                        "Redeemed voucher refund override created. OverrideId={OverrideId}, StoreOrderId={StoreOrderId}, OrderId={OrderId}, AdminUserId={AdminUserId}, CorrelationId={CorrelationId}",
                        value.Id,
                        value.StoreOrderId,
                        value.OrderId,
                        value.AdminUserId,
                        value.CorrelationId
                    );
                }
            }
        }

        var previous = await overrides.GetAttemptsAsync(value.Id, ct);
        if (previous.Any(x => x.Outcome == "RefundCompleted"))
            return await GetAdminStoreOrderRefundHandler.MapOverrideAsync(value, overrides, ct);

        try
        {
            var result = await mediator.Send(
                new CancelOrderCommand(
                    value.OrderId,
                    value.Reason,
                    $"voucher-refund-override-{value.Id:N}",
                    value.Id
                ),
                ct
            );
            if (!string.Equals(result.PaymentAction, "Refunded", StringComparison.Ordinal))
                throw new VoucherApplicationException(
                    "VOUCHER_REFUND_NOT_COMPLETED",
                    "نتیجه مالی سفارش بازگشت وجه تکمیل‌شده را تایید نمی‌کند"
                );
            await AppendAttemptAsync(
                value.Id,
                "RefundCompleted",
                result.PaymentAction,
                null,
                null,
                ct
            );
            logger.LogInformation(
                "Redeemed voucher refund override completed. OverrideId={OverrideId}, StoreOrderId={StoreOrderId}, OrderId={OrderId}, CorrelationId={CorrelationId}, PaymentAction={PaymentAction}",
                value.Id,
                value.StoreOrderId,
                value.OrderId,
                value.CorrelationId,
                result.PaymentAction
            );
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var code = ex is VoucherApplicationException voucherException
                ? voucherException.Code
                : "REFUND_RECONCILIATION_REQUIRED";
            await AppendAttemptAsync(
                value.Id,
                "ReconciliationRequired",
                null,
                code,
                "بازگشت وجه تکمیل نشد و نیازمند تلاش مجدد یا بررسی عملیاتی است",
                ct
            );
            logger.LogError(
                ex,
                "Redeemed voucher refund override requires reconciliation. OverrideId={OverrideId}, StoreOrderId={StoreOrderId}, OrderId={OrderId}, CorrelationId={CorrelationId}, FailureCode={FailureCode}",
                value.Id,
                value.StoreOrderId,
                value.OrderId,
                value.CorrelationId,
                code
            );
        }
        return await GetAdminStoreOrderRefundHandler.MapOverrideAsync(value, overrides, ct);
    }

    private async Task AppendAttemptAsync(
        Guid overrideId,
        string outcome,
        string? paymentAction,
        string? failureCode,
        string? failureMessage,
        CancellationToken ct
    )
    {
        for (var retry = 0; retry < 3; retry++)
        {
            var attempts = await overrides.GetAttemptsAsync(overrideId, ct);
            if (outcome == "RefundCompleted" && attempts.Any(x => x.Outcome == outcome))
                return;
            var sequence = attempts.Count == 0 ? 1 : attempts.Max(x => x.SequenceNumber) + 1;
            try
            {
                await overrides.AddAttemptAsync(
                    VoucherRefundOverrideAttempt.Create(
                        overrideId,
                        sequence,
                        outcome,
                        paymentAction,
                        failureCode,
                        failureMessage,
                        timeProvider.GetUtcNow()
                    ),
                    ct
                );
                return;
            }
            catch (StoreDomainException ex)
                when (ex.ErrorCode == "VOUCHER_REFUND_ATTEMPT_CONFLICT" && retry < 2)
            {
                // A concurrent retry consumed the sequence; reload and append the missing outcome.
            }
        }
    }
}

internal static class VoucherCode
{
    public static string Normalize(string code)
    {
        var normalized = code
            ?.Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 128)
            throw new VoucherApplicationException(
                "VOUCHER_NOT_REDEEMABLE",
                "کد ووچر معتبر یا قابل استفاده نیست"
            );
        return normalized;
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
