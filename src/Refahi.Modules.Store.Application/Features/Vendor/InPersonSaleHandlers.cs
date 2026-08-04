using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using Refahi.Shared.Services.Notification;

namespace Refahi.Modules.Store.Application.Features.Vendor;

internal static class InPersonOrderMapping
{
    public static InPersonOrderDto ToDto(OrderDto order, string? mobile = null,
        string? reference = null, DateTimeOffset? expiresAt = null) => new(
            order.Id, order.OrderNumber, order.Status, order.PaymentState,
            order.FinalAmountMinor, mobile is null ? null : Mask(mobile), reference, expiresAt,
            order.Items.SingleOrDefault()?.SourceItemId,
            order.Items.SingleOrDefault()?.Title,
            order.GrossAmountMinor,
            order.CommissionPercent,
            order.CommissionAmountMinor,
            order.VatPercent,
            order.VatAmountMinor,
            order.RecipientNetAmountMinor);

    public static string Mask(string mobile) => mobile.Length < 8 ? "***" : $"{mobile[..4]}***{mobile[^4..]}";

    public static string NormalizeMobile(string value)
    {
        if (!MobileNumberSearchNormalizer.TryNormalize(value, out var normalized) || string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("شماره موبایل معتبر نیست");
        if (normalized.StartsWith("98") && normalized.Length == 12) normalized = $"0{normalized[2..]}";
        if (normalized.Length != 11 || !normalized.StartsWith("09"))
            throw new InvalidOperationException("شماره موبایل معتبر نیست");
        return normalized;
    }

    public static async Task<OrderDto> GetOrderAsync(Guid orderId, IMediator mediator, CancellationToken ct) =>
        await mediator.Send(new GetOrderByIdQuery(orderId, Guid.Empty, "Admin"), ct)
        ?? throw new KeyNotFoundException("سفارش فروش حضوری یافت نشد");

    public static void EnsureInPerson(OrderDto order)
    {
        if (!string.Equals(order.ReferenceType, "StoreInPerson", StringComparison.OrdinalIgnoreCase) ||
            !order.SourceOwnerId.HasValue || !order.SourceShopId.HasValue)
            throw new InvalidOperationException("سفارش از نوع فروش حضوری نیست");
    }
}

public sealed class GetInPersonProductsHandler(
    IShopRepository shops,
    IShopProductRepository shopProducts,
    IProductRepository products,
    IMediator mediator) : IRequestHandler<GetInPersonProductsQuery, IReadOnlyList<InPersonProductDto>>
{
    public async Task<IReadOnlyList<InPersonProductDto>> Handle(GetInPersonProductsQuery request, CancellationToken ct)
    {
        var shop = await shops.GetByIdAsync(request.ShopId, ct) ?? throw new KeyNotFoundException("فروشگاه یافت نشد");
        if (shop.ShopType != ShopType.Physical || shop.Status != ShopStatus.Active)
            throw new InvalidOperationException("فروشگاه حضوری فعال نیست");
        if (!await mediator.Send(new AuthorizeStoreResourceQuery(request.VendorUserId, shop.SupplierId, shop.Id,
                StorePermissions.CreateInPersonOrder), ct))
            throw new UnauthorizedAccessException("دسترسی فروش حضوری برای این فروشگاه وجود ندارد");

        var (mappings, _) = await shopProducts.GetByShopAsync(shop.Id, true, 1, 500, ct);
        var productList = await products.GetByIdsAsync(mappings.Select(x => x.ProductId).Distinct().ToList(), ct);
        var result = new List<InPersonProductDto>();
        foreach (var product in productList.Where(x => x.IsAvailable && !x.IsDeleted))
        {
            var agreementProduct = await mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), ct);
            if (agreementProduct is null || agreementProduct.IsDeleted ||
                agreementProduct.DeliveryType != (short)DeliveryType.InPerson ||
                agreementProduct.PricingMode != (short)PricingMode.Manual ||
                agreementProduct.SalesModel != (short)SalesModel.Unlimited ||
                agreementProduct.SupplierId != shop.SupplierId) continue;
            result.Add(new InPersonProductDto(product.Id, product.Title,
                agreementProduct.CategoryName, agreementProduct.Id));
        }
        return result.OrderBy(x => x.Title).ToList();
    }
}

public sealed class StartInPersonOrderHandler(IShopRepository shops, INotificationService notification,
    IShopProductRepository shopProducts, IProductRepository products,
    IStoreInPersonFinancialPlanner financialPlanner,
    IInPersonOtpReferenceProtector otpReferences, IMediator mediator)
    : IRequestHandler<StartInPersonOrderCommand, InPersonOrderDto>
{
    public async Task<InPersonOrderDto> Handle(StartInPersonOrderCommand request, CancellationToken ct)
    {
        if (request.AmountMinor <= 0) throw new InvalidOperationException("مبلغ فروش باید بیشتر از صفر باشد");
        var shop = await shops.GetByIdAsync(request.ShopId, ct) ?? throw new KeyNotFoundException("فروشگاه یافت نشد");
        if (shop.ShopType != ShopType.Physical || shop.Status != ShopStatus.Active)
            throw new InvalidOperationException("فروش حضوری فقط برای فروشگاه حضوری فعال مجاز است");
        if (!await mediator.Send(new AuthorizeStoreResourceQuery(request.VendorUserId, shop.SupplierId, shop.Id,
                StorePermissions.CreateInPersonOrder), ct))
            throw new UnauthorizedAccessException("دسترسی فروش حضوری برای این فروشگاه وجود ندارد");

        var product = await products.GetByIdAsync(request.ProductId, ct)
            ?? throw new KeyNotFoundException("محصول حضوری یافت نشد");
        var mapping = await shopProducts.GetAsync(shop.Id, product.Id, ct);
        if (mapping is null || !mapping.IsActive || product.IsDeleted || !product.IsAvailable)
            throw new InvalidOperationException("محصول برای این فروشگاه فعال نیست");
        var agreementProduct = await mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), ct)
            ?? throw new KeyNotFoundException("محصول قرارداد یافت نشد");
        if (agreementProduct.DeliveryType != (short)DeliveryType.InPerson ||
            agreementProduct.PricingMode != (short)PricingMode.Manual ||
            agreementProduct.SalesModel != (short)SalesModel.Unlimited ||
            agreementProduct.SupplierId != shop.SupplierId)
            throw new InvalidOperationException("محصول انتخاب‌شده از نوع فروش حضوری نیست");

        var financial = await financialPlanner.BuildAsync(shop.SupplierId, request.AmountMinor,
            agreementProduct.CommissionPercent, agreementProduct.VatApplicable, ct);

        var mobile = InPersonOrderMapping.NormalizeMobile(request.MobileNumber);
        var buyer = (await mediator.Send(new GetOrderUserSummariesQuery(MobileNumber: mobile), ct))
            .SingleOrDefault(x => x.MobileNumber == mobile)
            ?? throw new KeyNotFoundException("کاربر فعالی با این شماره موبایل یافت نشد");

        var created = await mediator.Send(new CreateOrderCommand(
            UserId: buyer.UserId, SourceModule: "Store", SourceReferenceId: null,
            Items: [new CreateOrderItemInput(product.Title, request.AmountMinor, 1, 0,
                product.Id, "store.in-person", null, null)],
            IdempotencyKey: $"store-in-person:{request.VendorUserId:N}:{request.IdempotencyKey.Trim()}",
            ReferenceType: "StoreInPerson", SourceOwnerId: shop.SupplierId,
            SourceShopId: shop.Id, CreatedByUserId: request.VendorUserId,
            FinancialSnapshot: new OrderFinancialSnapshotInput(
                financial.GrossAmountMinor, financial.CommissionPercent,
                financial.CommissionAmountMinor, financial.VatPercent,
                financial.VatAmountMinor, financial.VendorNetAmountMinor),
            PaymentPostings: financial.Postings), ct);
        var order = await InPersonOrderMapping.GetOrderAsync(created.OrderId, mediator, ct);
        var challenge = await notification.SendOtp(mobile, OtpReceiptType.Sms,
            OtpType.VendorInPersonPayment, ct);
        var protectedReference = otpReferences.Protect(new(
            order.Id, shop.Id, mobile, challenge.ReferenceCode));
        return InPersonOrderMapping.ToDto(order, mobile, protectedReference, challenge.ExpiresAt);
    }
}

public sealed class VerifyInPersonOrderHandler(INotificationService notification,
    IInPersonOtpReferenceProtector otpReferences, IMediator mediator)
    : IRequestHandler<VerifyInPersonOrderCommand, InPersonOrderDto>
{
    public async Task<InPersonOrderDto> Handle(VerifyInPersonOrderCommand request, CancellationToken ct)
    {
        var order = await InPersonOrderMapping.GetOrderAsync(request.OrderId, mediator, ct);
        InPersonOrderMapping.EnsureInPerson(order);
        if (!await mediator.Send(new AuthorizeStoreResourceQuery(request.VendorUserId,
                order.SourceOwnerId!.Value, order.SourceShopId, StorePermissions.CreateInPersonOrder), ct))
            throw new UnauthorizedAccessException("دسترسی به این فروش وجود ندارد");
        var user = (await mediator.Send(new GetOrderUserSummariesQuery([order.UserId]), ct)).Single();
        var mobile = InPersonOrderMapping.NormalizeMobile(user.MobileNumber ?? string.Empty);
        if (order.PaymentState == "Paid") return InPersonOrderMapping.ToDto(order, mobile);

        if (order.PaymentState != "Reserved")
        {
            if (!otpReferences.TryUnprotect(request.OtpReferenceCode, out var otpReference) ||
                otpReference is null || otpReference.OrderId != order.Id ||
                otpReference.ShopId != order.SourceShopId ||
                !string.Equals(InPersonOrderMapping.NormalizeMobile(otpReference.MobileNumber), mobile,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("مرجع کد تایید متعلق به این سفارش نیست");

            var validation = await notification.ValidateOtp(otpReference.ProviderReferenceCode, request.OtpCode,
                OtpType.VendorInPersonPayment, ct);
            if (!validation.IsValid || (!string.IsNullOrWhiteSpace(validation.Receipt) &&
                InPersonOrderMapping.NormalizeMobile(validation.Receipt) != mobile))
                throw new InvalidOperationException("کد تایید نامعتبر یا منقضی است");
        }

        var options = await mediator.Send(new GetOrderPaymentOptionsQuery(order.Id, order.UserId, "Admin"), ct)
            ?? throw new InvalidOperationException("گزینه‌های پرداخت در دسترس نیست");
        if (!options.IsCovered) throw new InvalidOperationException("موجودی کیف پول‌های مجاز کاربر کافی نیست");
        await mediator.Send(new PayOrderCommand(order.Id, order.UserId, "User",
            options.Allocations.Select(x => new WalletAllocationInput(x.WalletId, x.AmountMinor)).ToList(),
            $"store-in-person-pay:{order.Id:N}:{request.IdempotencyKey.Trim()}"), ct);
        order = await InPersonOrderMapping.GetOrderAsync(order.Id, mediator, ct);
        return InPersonOrderMapping.ToDto(order, mobile);
    }
}

public sealed class ResendInPersonOrderOtpHandler(INotificationService notification,
    IInPersonOtpReferenceProtector otpReferences, IMediator mediator)
    : IRequestHandler<ResendInPersonOrderOtpCommand, InPersonOrderDto>
{
    public async Task<InPersonOrderDto> Handle(ResendInPersonOrderOtpCommand request, CancellationToken ct)
    {
        var order = await InPersonOrderMapping.GetOrderAsync(request.OrderId, mediator, ct);
        InPersonOrderMapping.EnsureInPerson(order);
        if (order.PaymentState == "Paid") throw new InvalidOperationException("سفارش قبلاً پرداخت شده است");
        if (!await mediator.Send(new AuthorizeStoreResourceQuery(request.VendorUserId,
                order.SourceOwnerId!.Value, order.SourceShopId, StorePermissions.CreateInPersonOrder), ct))
            throw new UnauthorizedAccessException("دسترسی به این فروش وجود ندارد");
        var user = (await mediator.Send(new GetOrderUserSummariesQuery([order.UserId]), ct)).Single();
        var mobile = InPersonOrderMapping.NormalizeMobile(user.MobileNumber ?? string.Empty);
        var challenge = await notification.SendOtp(mobile, OtpReceiptType.Sms, OtpType.VendorInPersonPayment, ct);
        var protectedReference = otpReferences.Protect(new(
            order.Id, order.SourceShopId!.Value, mobile, challenge.ReferenceCode));
        return InPersonOrderMapping.ToDto(order, mobile, protectedReference, challenge.ExpiresAt);
    }
}

public sealed class CancelInPersonOrderHandler(IMediator mediator)
    : IRequestHandler<CancelInPersonOrderCommand, InPersonOrderDto>
{
    public async Task<InPersonOrderDto> Handle(CancelInPersonOrderCommand request, CancellationToken ct)
    {
        var order = await InPersonOrderMapping.GetOrderAsync(request.OrderId, mediator, ct);
        InPersonOrderMapping.EnsureInPerson(order);
        var permission = order.PaymentState == "Paid"
            ? StorePermissions.RefundInPersonOrder : StorePermissions.CreateInPersonOrder;
        if (!await mediator.Send(new AuthorizeStoreResourceQuery(request.VendorUserId,
                order.SourceOwnerId!.Value, order.SourceShopId, permission), ct))
            throw new UnauthorizedAccessException("دسترسی لغو یا بازگشت وجه این سفارش وجود ندارد");
        await mediator.Send(new CancelOrderCommand(order.Id, "لغو فروش حضوری",
            $"store-in-person-cancel:{order.Id:N}:{request.IdempotencyKey.Trim()}"), ct);
        return InPersonOrderMapping.ToDto(await InPersonOrderMapping.GetOrderAsync(order.Id, mediator, ct));
    }
}
