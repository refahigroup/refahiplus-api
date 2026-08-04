using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Vendor;

public sealed record VendorOrderSummaryDto(
    Guid Id, string OrderNumber, long FinalAmountMinor, string Currency,
    string Status, string PaymentState, string ReferenceType,
    Guid ShopId, string? ShopName, string? MobileNumber, DateTimeOffset CreatedAt);

public sealed record VendorOrderDetailDto(
    Guid Id, string OrderNumber, long FinalAmountMinor, string Currency,
    string Status, string PaymentState, string ReferenceType,
    Guid ShopId, string? MobileNumber, DateTimeOffset CreatedAt,
    IReadOnlyList<VendorOrderItemDto> Items);

public sealed record VendorOrderItemDto(
    Guid Id, string Title, long UnitPriceMinor, int Quantity, long FinalPriceMinor);

public sealed record VendorOrderPagedResult<T>(
    IReadOnlyList<T> Data, int PageNumber, int PageSize, int TotalCount, int TotalPages);

public sealed record GetVendorOrdersQuery(
    Guid UserId, int Page, int PageSize, string? Status = null,
    string? PaymentState = null, string? OrderNumber = null,
    string? MobileNumber = null, Guid? ShopId = null,
    DateTimeOffset? From = null, DateTimeOffset? To = null)
    : IRequest<VendorOrderPagedResult<VendorOrderSummaryDto>>;

public sealed record GetVendorOrderByIdQuery(Guid UserId, Guid OrderId)
    : IRequest<VendorOrderDetailDto?>;

public sealed record UpdateVendorOrderStatusCommand(Guid UserId, Guid OrderId, string Status)
    : IRequest<VendorOrderDetailDto?>;

public sealed record VendorOrderDashboardDto(
    long IncomeWalletBalanceMinor, long TodaySalesMinor,
    int PendingOrders, int ProcessingOrders);

public sealed record GetVendorOrderDashboardQuery(Guid UserId)
    : IRequest<VendorOrderDashboardDto>;
