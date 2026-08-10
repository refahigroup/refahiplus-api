using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Vendor;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;

namespace Refahi.Modules.Orders.Application.Features.Vendor;

internal sealed record VendorOrderScope(
    Guid[] VendorIds,
    Guid[] ShopIds,
    Guid[] OwnShopIds,
    Dictionary<Guid, string> Shops
);

internal static class VendorOrderAccess
{
    public static async Task<VendorOrderScope> ResolveAsync(
        Guid userId,
        IMediator mediator,
        CancellationToken ct
    )
    {
        var contexts = await mediator.Send(new GetStoreVendorContextsQuery(userId), ct);
        var vendorIds = contexts
            .Where(x => x.Permissions.Contains(StorePermissions.ViewOrders))
            .Select(x => x.VendorId)
            .Distinct()
            .ToArray();
        var shopIds = contexts
            .SelectMany(x => x.Shops)
            .Where(x => x.Permissions.Contains(StorePermissions.ViewOrders))
            .Select(x => x.Id)
            .Distinct()
            .ToArray();
        var ownShopIds = contexts
            .SelectMany(x => x.Shops)
            .Where(x =>
                x.Permissions.Contains(StorePermissions.ViewOwnOrders)
                && !x.Permissions.Contains(StorePermissions.ViewOrders)
            )
            .Select(x => x.Id)
            .Distinct()
            .ToArray();
        var shops = contexts
            .SelectMany(x => x.Shops)
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First().Name);
        return new(vendorIds, shopIds, ownShopIds, shops);
    }

    public static VendorOrderDetailDto Detail(Order order, string? mobile) =>
        new(
            order.Id,
            order.OrderNumber,
            order.FinalAmountMinor,
            order.Currency,
            order.Status.ToString(),
            order.PaymentState.ToString(),
            order.ReferenceType,
            order.SourceShopId ?? Guid.Empty,
            Mask(mobile),
            order.CreatedAt,
            order
                .Items.Select(x => new VendorOrderItemDto(
                    x.Id,
                    x.Title,
                    x.UnitPriceMinor,
                    x.Quantity,
                    x.FinalPriceMinor
                ))
                .ToArray()
        );

    public static string? Mask(string? mobile) =>
        string.IsNullOrWhiteSpace(mobile) || mobile.Length < 7
            ? null
            : $"{mobile[..4]}***{mobile[^4..]}";
}

public sealed class GetVendorOrdersHandler(IOrderRepository repository, IMediator mediator)
    : IRequestHandler<GetVendorOrdersQuery, VendorOrderPagedResult<VendorOrderSummaryDto>>
{
    public async Task<VendorOrderPagedResult<VendorOrderSummaryDto>> Handle(
        GetVendorOrdersQuery request,
        CancellationToken ct
    )
    {
        var scope = await VendorOrderAccess.ResolveAsync(request.UserId, mediator, ct);
        if (request.ShopId.HasValue && !scope.Shops.ContainsKey(request.ShopId.Value))
            return new([], Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), 0, 0);
        IReadOnlyCollection<Guid>? customerIds = null;
        if (!string.IsNullOrWhiteSpace(request.MobileNumber))
            customerIds = (
                await mediator.Send(
                    new GetOrderUserSummariesQuery(MobileNumber: request.MobileNumber),
                    ct
                )
            )
                .Select(x => x.UserId)
                .ToArray();
        var (orders, total) = await repository.GetVendorOrdersAsync(
            scope.VendorIds,
            scope.ShopIds,
            scope.OwnShopIds,
            request.UserId,
            request.Page,
            request.PageSize,
            request.Status,
            request.PaymentState,
            request.OrderNumber,
            customerIds,
            request.ShopId,
            request.From,
            request.To,
            ct
        );
        var users = await mediator.Send(
            new GetOrderUserSummariesQuery(orders.Select(x => x.UserId).Distinct().ToArray()),
            ct
        );
        var mobiles = users.ToDictionary(x => x.UserId, x => x.MobileNumber);
        var data = orders
            .Select(order =>
            {
                mobiles.TryGetValue(order.UserId, out var mobile);
                scope.Shops.TryGetValue(order.SourceShopId ?? Guid.Empty, out var shopName);
                return new VendorOrderSummaryDto(
                    order.Id,
                    order.OrderNumber,
                    order.FinalAmountMinor,
                    order.Currency,
                    order.Status.ToString(),
                    order.PaymentState.ToString(),
                    order.ReferenceType,
                    order.SourceShopId ?? Guid.Empty,
                    shopName,
                    VendorOrderAccess.Mask(mobile),
                    order.CreatedAt
                );
            })
            .ToArray();
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        return new(
            data,
            Math.Max(1, request.Page),
            pageSize,
            total,
            (int)Math.Ceiling(total / (double)pageSize)
        );
    }
}

public sealed class GetVendorOrderByIdHandler(IOrderRepository repository, IMediator mediator)
    : IRequestHandler<GetVendorOrderByIdQuery, VendorOrderDetailDto?>
{
    public async Task<VendorOrderDetailDto?> Handle(
        GetVendorOrderByIdQuery request,
        CancellationToken ct
    )
    {
        var scope = await VendorOrderAccess.ResolveAsync(request.UserId, mediator, ct);
        var order = await repository.GetVendorOrderByIdAsync(
            request.OrderId,
            scope.VendorIds,
            scope.ShopIds,
            scope.OwnShopIds,
            request.UserId,
            ct
        );
        if (order is null)
            return null;
        var user = (
            await mediator.Send(new GetOrderUserSummariesQuery([order.UserId]), ct)
        ).SingleOrDefault();
        return VendorOrderAccess.Detail(order, user?.MobileNumber);
    }
}

public sealed class UpdateVendorOrderStatusHandler(IOrderRepository repository, IMediator mediator)
    : IRequestHandler<UpdateVendorOrderStatusCommand, VendorOrderDetailDto?>
{
    public async Task<VendorOrderDetailDto?> Handle(
        UpdateVendorOrderStatusCommand request,
        CancellationToken ct
    )
    {
        var scope = await VendorOrderAccess.ResolveAsync(request.UserId, mediator, ct);
        var order = await repository.GetVendorOrderByIdAsync(
            request.OrderId,
            scope.VendorIds,
            scope.ShopIds,
            scope.OwnShopIds,
            request.UserId,
            ct
        );
        if (order is null || !order.SourceOwnerId.HasValue || !order.SourceShopId.HasValue)
            return null;
        if (
            !await mediator.Send(
                new AuthorizeStoreResourceQuery(
                    request.UserId,
                    order.SourceOwnerId.Value,
                    order.SourceShopId,
                    StorePermissions.ManageOrders
                ),
                ct
            )
        )
            throw new UnauthorizedAccessException("دسترسی تغییر وضعیت سفارش وجود ندارد");
        if (order.ReferenceType.Equals("StoreInPerson", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("وضعیت سفارش حضوری از جریان پرداخت تعیین می‌شود");
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var status))
            throw new InvalidOperationException("وضعیت سفارش معتبر نیست");
        order.UpdateStatus(status);
        await repository.UpdateAsync(order, ct);
        var user = (
            await mediator.Send(new GetOrderUserSummariesQuery([order.UserId]), ct)
        ).SingleOrDefault();
        return VendorOrderAccess.Detail(order, user?.MobileNumber);
    }
}

public sealed class GetVendorOrderDashboardHandler(IOrderRepository repository, IMediator mediator)
    : IRequestHandler<GetVendorOrderDashboardQuery, VendorOrderDashboardDto>
{
    public async Task<VendorOrderDashboardDto> Handle(
        GetVendorOrderDashboardQuery request,
        CancellationToken ct
    )
    {
        var contexts = await mediator.Send(new GetStoreVendorContextsQuery(request.UserId), ct);
        var vendorIds = contexts
            .Where(x => x.Permissions.Contains(StorePermissions.ViewOrders))
            .Select(x => x.VendorId)
            .Distinct()
            .ToArray();
        var shopIds = contexts
            .SelectMany(x => x.Shops)
            .Where(x => x.Permissions.Contains(StorePermissions.ViewOrders))
            .Select(x => x.Id)
            .Distinct()
            .ToArray();
        var ownShopIds = contexts
            .SelectMany(x => x.Shops)
            .Where(x =>
                x.Permissions.Contains(StorePermissions.ViewOwnOrders)
                && !x.Permissions.Contains(StorePermissions.ViewOrders)
            )
            .Select(x => x.Id)
            .Distinct()
            .ToArray();
        var counts =
            vendorIds.Length == 0 && shopIds.Length == 0 && ownShopIds.Length == 0
                ? (Pending: 0, Processing: 0)
                : await repository.GetVendorStatusCountsAsync(
                    vendorIds,
                    shopIds,
                    ownShopIds,
                    request.UserId,
                    ct
                );
        long balance = 0;
        long todayIncome = 0;
        var today = DateTimeOffset.UtcNow.Date;
        foreach (
            var vendor in contexts.Where(x =>
                x.Permissions.Contains(StorePermissions.ViewIncomeWallet)
            )
        )
        {
            balance += (await mediator.Send(new GetMyWalletsQuery(vendor.VendorId), ct))
                .Where(x => x.WalletType.Equals("Provider", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.AvailableBalanceMinor);
            todayIncome += (
                await mediator.Send(
                    new GetMyWalletTransactionsQuery(vendor.VendorId, 100, "Provider", 3, 1),
                    ct
                )
            )
                .Where(x => x.CreatedAt >= today)
                .Sum(x => x.AmountMinor);
        }
        return new(balance, todayIncome, counts.Pending, counts.Processing);
    }
}
