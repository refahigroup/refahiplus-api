using MediatR;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Vendor;

internal static class VendorStoreOrderReadMapping
{
    public static VendorStoreOrderSnapshotDto Map(StoreOrder order)
    {
        var items = order
            .Items.Select(x => new VendorStoreOrderItemSnapshotDto(
                x.Id,
                x.ProductId,
                x.OfferId,
                x.ProductTitle,
                x.CategoryId,
                x.CategoryCode,
                x.SupplierId,
                x.ShopId,
                x.SalesChannel.ToString(),
                x.ProductType.ToString(),
                x.SalesModel.ToString(),
                x.FulfillmentMethod.ToString(),
                x.Quantity,
                x.UnitPriceMinor,
                x.GrossAmountMinor,
                x.DeclaredGrossAmountMinor,
                x.AgreementId,
                x.AgreementCategoryTermId,
                x.CommissionPercent,
                x.CommissionAmountMinor
            ))
            .ToArray();
        return new(
            order.Id,
            order.OrderId!.Value,
            order.UserId,
            order.CreatedByUserId,
            order.SalesChannel.ToString(),
            order.InitiatorType,
            order.Status.ToString(),
            order.SupplierId,
            order.ShopId,
            order.FinalAmountMinor,
            items.Length == 1 ? items[0].DeclaredGrossAmountMinor : null,
            items
        );
    }

    public static bool CanRead(
        StoreOrder order,
        Guid actorId,
        IReadOnlyList<StoreVendorContextDto> contexts
    )
    {
        var vendor = contexts.SingleOrDefault(x => x.VendorId == order.SupplierId);
        if (vendor is null)
            return false;
        if (
            vendor.Permissions.Contains(
                StorePermissions.ViewOrders,
                StringComparer.OrdinalIgnoreCase
            )
        )
            return true;
        var shop = vendor.Shops.SingleOrDefault(x => x.Id == order.ShopId);
        if (
            shop?.Permissions.Contains(
                StorePermissions.ViewOrders,
                StringComparer.OrdinalIgnoreCase
            ) == true
        )
            return true;
        return shop?.Permissions.Contains(
                StorePermissions.ViewOwnOrders,
                StringComparer.OrdinalIgnoreCase
            ) == true
            && order.CreatedByUserId == actorId;
    }
}

public sealed class GetVendorStoreOrderByOrderIdHandler(
    IStoreOrderRepository orders,
    IMediator mediator
) : IRequestHandler<GetVendorStoreOrderByOrderIdQuery, VendorStoreOrderSnapshotDto?>
{
    public async Task<VendorStoreOrderSnapshotDto?> Handle(
        GetVendorStoreOrderByOrderIdQuery request,
        CancellationToken ct
    )
    {
        var order = await orders.GetByOrderIdAsync(request.OrderId, ct);
        if (order is null)
            return null;
        var contexts = await mediator.Send(
            new GetStoreVendorContextsQuery(request.VendorUserId),
            ct
        );
        return VendorStoreOrderReadMapping.CanRead(order, request.VendorUserId, contexts)
            ? VendorStoreOrderReadMapping.Map(order)
            : null;
    }
}

public sealed class GetVendorStoreOrdersByOrderIdsHandler(
    IStoreOrderRepository orders,
    IMediator mediator
) : IRequestHandler<GetVendorStoreOrdersByOrderIdsQuery, IReadOnlyList<VendorStoreOrderSnapshotDto>>
{
    public async Task<IReadOnlyList<VendorStoreOrderSnapshotDto>> Handle(
        GetVendorStoreOrdersByOrderIdsQuery request,
        CancellationToken ct
    )
    {
        var ids = request.OrderIds.Where(x => x != Guid.Empty).Distinct().Take(100).ToArray();
        if (ids.Length == 0)
            return [];
        var rows = await orders.GetByOrderIdsAsync(ids, ct);
        var contexts = await mediator.Send(
            new GetStoreVendorContextsQuery(request.VendorUserId),
            ct
        );
        var byOrderId = rows.Where(x =>
                x.OrderId.HasValue
                && VendorStoreOrderReadMapping.CanRead(x, request.VendorUserId, contexts)
            )
            .ToDictionary(x => x.OrderId!.Value);
        return ids.Where(byOrderId.ContainsKey)
            .Select(x => VendorStoreOrderReadMapping.Map(byOrderId[x]))
            .ToArray();
    }
}

public sealed class GetUserStoreOrderByOrderIdHandler(IStoreOrderRepository orders)
    : IRequestHandler<GetUserStoreOrderByOrderIdQuery, VendorStoreOrderSnapshotDto?>
{
    public async Task<VendorStoreOrderSnapshotDto?> Handle(
        GetUserStoreOrderByOrderIdQuery request,
        CancellationToken ct
    )
    {
        var order = await orders.GetByOrderIdAsync(request.OrderId, ct);
        if (order is null || (!request.IsAdmin && order.UserId != request.UserId))
            return null;
        return VendorStoreOrderReadMapping.Map(order);
    }
}

public sealed class GetUserStoreOrdersByOrderIdsHandler(IStoreOrderRepository orders)
    : IRequestHandler<GetUserStoreOrdersByOrderIdsQuery, IReadOnlyList<VendorStoreOrderSnapshotDto>>
{
    public async Task<IReadOnlyList<VendorStoreOrderSnapshotDto>> Handle(
        GetUserStoreOrdersByOrderIdsQuery request,
        CancellationToken ct
    )
    {
        var ids = request.OrderIds.Where(x => x != Guid.Empty).Distinct().Take(100).ToArray();
        if (ids.Length == 0)
            return [];
        var rows = await orders.GetByOrderIdsAsync(ids, ct);
        var byOrderId = rows.Where(x =>
                x.OrderId.HasValue && (request.IsAdmin || x.UserId == request.UserId)
            )
            .ToDictionary(x => x.OrderId!.Value);
        return ids.Where(byOrderId.ContainsKey)
            .Select(x => VendorStoreOrderReadMapping.Map(byOrderId[x]))
            .ToArray();
    }
}
