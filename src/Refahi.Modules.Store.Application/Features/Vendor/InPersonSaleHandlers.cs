using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementCategoryTerms;
using Refahi.Shared.Services.Notification;

namespace Refahi.Modules.Store.Application.Features.Vendor;

internal static class InPersonOrderMapping
{
    public static InPersonOrderDto ToDto(
        StoreOrder storeOrder,
        OrderDto order,
        string? mobile = null
    ) =>
        new(
            order.Id,
            order.OrderNumber,
            order.Status,
            order.PaymentState,
            order.FinalAmountMinor,
            mobile is null ? null : Mask(mobile),
            storeOrder.OtpReferenceCode,
            storeOrder.OtpExpiresAt,
            storeOrder.Items.Single().ProductId,
            storeOrder.Items.Single().ProductTitle,
            order.GrossAmountMinor,
            order.CommissionPercent,
            order.CommissionAmountMinor,
            order.VatPercent,
            order.VatAmountMinor,
            order.RecipientNetAmountMinor,
            storeOrder.Id,
            $"/checkout/orders/{order.Id}"
        );

    public static string Mask(string mobile) =>
        mobile.Length < 8 ? "***" : $"{mobile[..4]}***{mobile[^4..]}";

    public static string NormalizeMobile(string value)
    {
        if (
            !MobileNumberSearchNormalizer.TryNormalize(value, out var normalized)
            || string.IsNullOrWhiteSpace(normalized)
        )
            throw new StoreDomainException("شماره موبایل معتبر نیست", "MOBILE_INVALID");
        if (normalized.StartsWith("98") && normalized.Length == 12)
            normalized = $"0{normalized[2..]}";
        if (normalized.Length != 11 || !normalized.StartsWith("09"))
            throw new StoreDomainException("شماره موبایل معتبر نیست", "MOBILE_INVALID");
        return normalized;
    }

    public static async Task<OrderDto> GetOrderAsync(
        Guid orderId,
        IMediator mediator,
        CancellationToken ct
    ) =>
        await mediator.Send(new GetOrderByIdQuery(orderId, Guid.Empty, "Admin"), ct)
        ?? throw new StoreDomainException("سفارش فروش حضوری یافت نشد", "ORDER_NOT_FOUND");

    public static string Fingerprint(
        Guid userId,
        Guid actorId,
        string initiator,
        Guid shopId,
        Guid productId,
        long amount
    )
    {
        var value =
            $"in-person-v1|{userId:N}|{actorId:N}|{initiator}|{shopId:N}|{productId:N}|{amount}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}

internal sealed record SellableInPersonContext(
    Shop Shop,
    Product Product,
    ResolvedAgreementCategoryTermDto Term,
    string CategoryCode,
    string? CategoryName
);

internal static class InPersonEligibility
{
    public static async Task<SellableInPersonContext> ResolveAsync(
        Guid shopId,
        Guid productId,
        IShopRepository shops,
        IProductRepository products,
        IMediator mediator,
        TimeProvider clock,
        CancellationToken ct
    )
    {
        var shop =
            await shops.GetByIdAsync(shopId, ct)
            ?? throw new StoreDomainException("فروشگاه یافت نشد", "SHOP_NOT_FOUND");
        if (shop.Channel != SalesChannel.InPerson)
            throw new StoreDomainException(
                "فروشگاه انتخابی حضوری نیست",
                "SHOP_CHANNEL_NOT_ALLOWED"
            );
        if (shop.Status != ShopStatus.Active)
            throw new StoreDomainException("فروشگاه حضوری فعال نیست", "SHOP_NOT_ACTIVE");

        var product =
            await products.GetByIdAsync(productId, ct)
            ?? throw new StoreDomainException("محصول حضوری یافت نشد", "PRODUCT_NOT_FOUND");
        if (product.IsDeleted || !product.IsAvailable)
            throw new StoreDomainException("محصول فعال نیست", "PRODUCT_NOT_ACTIVE");
        if (product.SupplierId == Guid.Empty || product.SupplierId != shop.SupplierId)
            throw new StoreDomainException(
                "محصول متعلق به تامین‌کننده این فروشگاه نیست",
                "PRODUCT_SUPPLIER_MISMATCH"
            );

        var term =
            await mediator.Send(
                new ResolveAgreementCategoryTermQuery(
                    shop.SupplierId,
                    product.CategoryId,
                    (short)SalesChannel.InPerson,
                    clock.GetUtcNow()
                ),
                ct
            )
            ?? throw new StoreDomainException(
                "قرارداد موثر فروش حضوری یافت نشد",
                "AGREEMENT_TERM_NOT_EFFECTIVE"
            );
        var category =
            await mediator.Send(new GetCategoryByIdQuery(product.CategoryId), ct)
            ?? throw new StoreDomainException("دسته‌بندی محصول یافت نشد", "CATEGORY_NOT_FOUND");
        if (string.IsNullOrWhiteSpace(category.CategoryCode))
            throw new StoreDomainException(
                "کد دسته‌بندی محصول معتبر نیست",
                "CATEGORY_CODE_REQUIRED"
            );
        return new(shop, product, term, category.CategoryCode, category.Name);
    }

    public static async Task<IReadOnlyList<InPersonProductDto>> ListAsync(
        Guid shopId,
        IShopRepository shops,
        IProductRepository products,
        IMediator mediator,
        TimeProvider clock,
        CancellationToken ct
    )
    {
        var shop =
            await shops.GetByIdAsync(shopId, ct)
            ?? throw new StoreDomainException("فروشگاه یافت نشد", "SHOP_NOT_FOUND");
        if (shop.Channel != SalesChannel.InPerson || shop.Status != ShopStatus.Active)
            throw new StoreDomainException("فروشگاه حضوری فعال نیست", "SHOP_NOT_SELLABLE");
        var candidates = await products.GetCatalogEligibilityCandidatesAsync(
            shop.SupplierId,
            null,
            ct
        );
        var requests = candidates
            .Select(x => new AgreementCategoryTermResolutionRequest(
                shop.SupplierId,
                x.CategoryId,
                (short)SalesChannel.InPerson,
                clock.GetUtcNow()
            ))
            .ToList();
        var terms = await mediator.Send(new ResolveAgreementCategoryTermsBatchQuery(requests), ct);
        var result = new List<InPersonProductDto>();
        for (var i = 0; i < candidates.Count; i++)
        {
            var term = terms[i].Term;
            if (term is null)
                continue;
            var category = await mediator.Send(
                new GetCategoryByIdQuery(candidates[i].CategoryId),
                ct
            );
            result.Add(
                new(
                    candidates[i].Id,
                    candidates[i].Title,
                    category?.Name,
                    Guid.Empty,
                    candidates[i].CategoryId,
                    term.AgreementId,
                    term.TermId,
                    term.CommissionPercent
                )
            );
        }
        return result.OrderBy(x => x.Title).ToArray();
    }
}

public sealed class GetInPersonProductsHandler(
    IShopRepository shops,
    IProductRepository products,
    IMediator mediator,
    TimeProvider clock
) : IRequestHandler<GetInPersonProductsQuery, IReadOnlyList<InPersonProductDto>>
{
    public async Task<IReadOnlyList<InPersonProductDto>> Handle(
        GetInPersonProductsQuery request,
        CancellationToken ct
    )
    {
        var shop =
            await shops.GetByIdAsync(request.ShopId, ct)
            ?? throw new StoreDomainException("فروشگاه یافت نشد", "SHOP_NOT_FOUND");
        if (
            !await mediator.Send(
                new AuthorizeStoreResourceQuery(
                    request.VendorUserId,
                    shop.SupplierId,
                    shop.Id,
                    StorePermissions.CreateInPersonOrder
                ),
                ct
            )
        )
            throw new UnauthorizedAccessException("دسترسی فروش حضوری برای این فروشگاه وجود ندارد");
        return await InPersonEligibility.ListAsync(
            request.ShopId,
            shops,
            products,
            mediator,
            clock,
            ct
        );
    }
}

public sealed class GetUserInPersonProductsHandler(
    IShopRepository shops,
    IProductRepository products,
    IMediator mediator,
    TimeProvider clock
) : IRequestHandler<GetUserInPersonProductsQuery, IReadOnlyList<InPersonProductDto>>
{
    public Task<IReadOnlyList<InPersonProductDto>> Handle(
        GetUserInPersonProductsQuery request,
        CancellationToken ct
    ) => InPersonEligibility.ListAsync(request.ShopId, shops, products, mediator, clock, ct);
}

public sealed class GetUserInPersonShopsHandler(
    IShopRepository shops,
    IProductRepository products,
    IMediator mediator,
    TimeProvider clock
) : IRequestHandler<GetUserInPersonShopsQuery, IReadOnlyList<InPersonShopDto>>
{
    public async Task<IReadOnlyList<InPersonShopDto>> Handle(
        GetUserInPersonShopsQuery request,
        CancellationToken ct
    )
    {
        var (candidates, _) = await shops.GetPagedAsync(
            ShopType.InPerson,
            ShopStatus.Active,
            1,
            1000,
            ct
        );
        var result = new List<InPersonShopDto>();
        foreach (var shop in candidates)
            if (
                (
                    await InPersonEligibility.ListAsync(
                        shop.Id,
                        shops,
                        products,
                        mediator,
                        clock,
                        ct
                    )
                ).Count > 0
            )
                result.Add(new(shop.Id, shop.Name, shop.Slug, shop.SupplierId));
        return result;
    }
}

public sealed class StartInPersonOrderHandler(
    IShopRepository shops,
    IProductRepository products,
    IStoreOrderRepository storeOrders,
    IStoreInPersonFinancialPlanner financialPlanner,
    INotificationService notification,
    IInPersonOtpReferenceProtector otpReferences,
    IMediator mediator,
    TimeProvider clock,
    Microsoft.Extensions.Logging.ILogger<StartInPersonOrderHandler> logger
) : IRequestHandler<StartInPersonOrderCommand, InPersonOrderDto>
{
    public async Task<InPersonOrderDto> Handle(
        StartInPersonOrderCommand request,
        CancellationToken ct
    )
    {
        if (request.AmountMinor <= 0)
            throw new StoreDomainException(
                "مبلغ فروش باید بیشتر از صفر باشد",
                "INVALID_MANUAL_AMOUNT"
            );
        var mobile = InPersonOrderMapping.NormalizeMobile(request.MobileNumber);
        var buyer =
            (
                await mediator.Send(new GetOrderUserSummariesQuery(MobileNumber: mobile), ct)
            ).SingleOrDefault(x => x.MobileNumber == mobile)
            ?? throw new StoreDomainException(
                "کاربر فعالی با این شماره موبایل یافت نشد",
                "BUYER_NOT_FOUND"
            );
        var key = $"vendor:{request.VendorUserId:N}:{request.IdempotencyKey.Trim()}";
        var fingerprint = InPersonOrderMapping.Fingerprint(
            buyer.UserId,
            request.VendorUserId,
            "Vendor",
            request.ShopId,
            request.ProductId,
            request.AmountMinor
        );
        var existing = await storeOrders.GetByIdempotencyKeyAsync(buyer.UserId, key, ct);
        StoreOrder storeOrder;
        StoreInPersonFinancialPlan? financial = null;
        if (existing is not null)
        {
            existing.EnsureRequestFingerprint(fingerprint);
            storeOrder = existing;
            await VerifyInPersonOrderHandler.EnsurePermission(
                request.VendorUserId,
                storeOrder,
                StorePermissions.CreateInPersonOrder,
                mediator,
                ct
            );
            if (!storeOrder.OrderId.HasValue)
                financial = await financialPlanner.BuildAsync(
                    storeOrder.SupplierId,
                    storeOrder.FinalAmountMinor,
                    storeOrder.Items.Single().CommissionPercent,
                    true,
                    ct
                );
        }
        else
        {
            var context = await InPersonEligibility.ResolveAsync(
                request.ShopId,
                request.ProductId,
                shops,
                products,
                mediator,
                clock,
                ct
            );
            if (
                !await mediator.Send(
                    new AuthorizeStoreResourceQuery(
                        request.VendorUserId,
                        context.Shop.SupplierId,
                        context.Shop.Id,
                        StorePermissions.CreateInPersonOrder
                    ),
                    ct
                )
            )
                throw new UnauthorizedAccessException(
                    "دسترسی فروش حضوری برای این فروشگاه وجود ندارد"
                );
            financial = await financialPlanner.BuildAsync(
                context.Shop.SupplierId,
                request.AmountMinor,
                context.Term.CommissionPercent,
                true,
                ct
            );
            storeOrder = StoreOrder.CreateInPerson(
                buyer.UserId,
                request.VendorUserId,
                "Vendor",
                context.Shop.Id,
                context.Shop.SupplierId,
                key,
                fingerprint,
                Snapshot(context, request.AmountMinor)
            );
            try
            {
                await storeOrders.AddAsync(storeOrder, ct);
            }
            catch (StoreDomainException ex) when (ex.ErrorCode == "IDEMPOTENCY_CONFLICT")
            {
                storeOrder =
                    await storeOrders.GetByIdempotencyKeyAsync(buyer.UserId, key, ct)
                    ?? throw new StoreDomainException(
                        "سفارش هم‌زمان یافت نشد",
                        "IDEMPOTENCY_CONFLICT"
                    );
                storeOrder.EnsureRequestFingerprint(fingerprint);
                if (!storeOrder.OrderId.HasValue)
                    financial = await financialPlanner.BuildAsync(
                        storeOrder.SupplierId,
                        storeOrder.FinalAmountMinor,
                        storeOrder.Items.Single().CommissionPercent,
                        true,
                        ct
                    );
            }
        }

        var order = storeOrder.OrderId.HasValue
            ? await InPersonOrderMapping.GetOrderAsync(storeOrder.OrderId.Value, mediator, ct)
            : await ResumeOrderAsync(storeOrder, financial!, mediator, storeOrders, ct);
        if (order.PaymentState != "Paid" && string.IsNullOrWhiteSpace(storeOrder.OtpReferenceCode))
        {
            if (storeOrder.OtpDispatchStartedAt.HasValue)
                throw new StoreDomainException(
                    "وضعیت ارسال کد تایید نامشخص است؛ از گزینه ارسال مجدد استفاده کنید",
                    "OTP_DELIVERY_REQUIRES_RESEND"
                );
            storeOrder.BeginOtpDispatch();
            await storeOrders.UpdateAsync(storeOrder, ct);
            var challenge = await notification.SendOtp(
                mobile,
                OtpReceiptType.Sms,
                OtpType.VendorInPersonPayment,
                ct
            );
            var protectedReference = otpReferences.Protect(
                new(order.Id, storeOrder.ShopId, mobile, challenge.ReferenceCode)
            );
            storeOrder.AttachOtpChallenge(protectedReference, challenge.ExpiresAt);
            await storeOrders.UpdateAsync(storeOrder, ct);
        }
        logger.LogInformation(
            "In-person StoreOrder ready. Initiator={Initiator} SupplierId={SupplierId} ShopId={ShopId} ProductId={ProductId} StoreOrderId={StoreOrderId} OrderId={OrderId} IdempotencyKey={IdempotencyKey}",
            "Vendor",
            storeOrder.SupplierId,
            storeOrder.ShopId,
            storeOrder.Items.Single().ProductId,
            storeOrder.Id,
            order.Id,
            storeOrder.IdempotencyKey
        );
        return InPersonOrderMapping.ToDto(storeOrder, order, mobile);
    }

    internal static StoreOrderItemSnapshot Snapshot(SellableInPersonContext context, long amount) =>
        new(
            Guid.Empty,
            context.Product.Id,
            null,
            null,
            null,
            context.Product.Title,
            null,
            null,
            context.Product.CategoryId,
            context.CategoryCode,
            context.Shop.SupplierId,
            context.Shop.Id,
            SalesChannel.InPerson,
            context.Product.ProductType,
            context.Product.SalesModel,
            context.Product.FulfillmentMethod,
            1,
            amount,
            0,
            amount,
            context.Term.AgreementId,
            context.Term.TermId,
            context.Term.CommissionPercent,
            null,
            0,
            amount
        );

    internal static async Task<OrderDto> ResumeOrderAsync(
        StoreOrder storeOrder,
        StoreInPersonFinancialPlan financial,
        IMediator mediator,
        IStoreOrderRepository storeOrders,
        CancellationToken ct
    )
    {
        if (!storeOrder.OrderId.HasValue)
        {
            var item = storeOrder.Items.Single();
            var created = await mediator.Send(
                new CreateOrderCommand(
                    storeOrder.UserId,
                    "Store",
                    storeOrder.Id,
                    [
                        new CreateOrderItemInput(
                            item.ProductTitle,
                            item.DeclaredGrossAmountMinor!.Value,
                            1,
                            0,
                            item.Id,
                            item.CategoryCode,
                            ["store", "in-person", $"shop:{storeOrder.ShopId:N}"],
                            JsonSerializer.Serialize(
                                new
                                {
                                    store_order_id = storeOrder.Id,
                                    store_order_item_id = item.Id,
                                    product_id = item.ProductId,
                                    agreement_id = item.AgreementId,
                                    agreement_category_term_id = item.AgreementCategoryTermId,
                                    declared_gross_amount_minor = item.DeclaredGrossAmountMinor,
                                    commission_percent = item.CommissionPercent,
                                    commission_amount_minor = item.CommissionAmountMinor,
                                }
                            )
                        ),
                    ],
                    $"store-in-person-order:{storeOrder.Id:N}",
                    "StoreOrder",
                    SourceOwnerId: storeOrder.SupplierId,
                    SourceShopId: storeOrder.ShopId,
                    CreatedByUserId: storeOrder.CreatedByUserId,
                    FinancialSnapshot: new(
                        financial.GrossAmountMinor,
                        financial.CommissionPercent,
                        financial.CommissionAmountMinor,
                        financial.VatPercent,
                        financial.VatAmountMinor,
                        financial.VendorNetAmountMinor
                    ),
                    PaymentPostings: financial.Postings
                ),
                ct
            );
            storeOrder.AttachOrder(created.OrderId);
            await storeOrders.UpdateAsync(storeOrder, ct);
        }
        return await InPersonOrderMapping.GetOrderAsync(storeOrder.OrderId!.Value, mediator, ct);
    }
}

public sealed class StartUserInPersonOrderHandler(
    IShopRepository shops,
    IProductRepository products,
    IStoreOrderRepository storeOrders,
    IStoreInPersonFinancialPlanner financialPlanner,
    IMediator mediator,
    TimeProvider clock,
    Microsoft.Extensions.Logging.ILogger<StartUserInPersonOrderHandler> logger
) : IRequestHandler<StartUserInPersonOrderCommand, InPersonOrderDto>
{
    public async Task<InPersonOrderDto> Handle(
        StartUserInPersonOrderCommand request,
        CancellationToken ct
    )
    {
        if (request.AmountMinor <= 0)
            throw new StoreDomainException(
                "مبلغ فروش باید بیشتر از صفر باشد",
                "INVALID_MANUAL_AMOUNT"
            );
        var key = $"user:{request.UserId:N}:{request.IdempotencyKey.Trim()}";
        var fingerprint = InPersonOrderMapping.Fingerprint(
            request.UserId,
            request.UserId,
            "User",
            request.ShopId,
            request.ProductId,
            request.AmountMinor
        );
        var storeOrder = await storeOrders.GetByIdempotencyKeyAsync(request.UserId, key, ct);
        StoreInPersonFinancialPlan? financial = null;
        if (storeOrder is null)
        {
            var context = await InPersonEligibility.ResolveAsync(
                request.ShopId,
                request.ProductId,
                shops,
                products,
                mediator,
                clock,
                ct
            );
            financial = await financialPlanner.BuildAsync(
                context.Shop.SupplierId,
                request.AmountMinor,
                context.Term.CommissionPercent,
                true,
                ct
            );
            storeOrder = StoreOrder.CreateInPerson(
                request.UserId,
                request.UserId,
                "User",
                context.Shop.Id,
                context.Shop.SupplierId,
                key,
                fingerprint,
                StartInPersonOrderHandler.Snapshot(context, request.AmountMinor)
            );
            try
            {
                await storeOrders.AddAsync(storeOrder, ct);
            }
            catch (StoreDomainException ex) when (ex.ErrorCode == "IDEMPOTENCY_CONFLICT")
            {
                storeOrder =
                    await storeOrders.GetByIdempotencyKeyAsync(request.UserId, key, ct)
                    ?? throw new StoreDomainException(
                        "سفارش هم‌زمان یافت نشد",
                        "IDEMPOTENCY_CONFLICT"
                    );
                storeOrder.EnsureRequestFingerprint(fingerprint);
                if (!storeOrder.OrderId.HasValue)
                    financial = await financialPlanner.BuildAsync(
                        storeOrder.SupplierId,
                        storeOrder.FinalAmountMinor,
                        storeOrder.Items.Single().CommissionPercent,
                        true,
                        ct
                    );
            }
        }
        else
        {
            storeOrder.EnsureRequestFingerprint(fingerprint);
            if (!storeOrder.OrderId.HasValue)
                financial = await financialPlanner.BuildAsync(
                    storeOrder.SupplierId,
                    storeOrder.FinalAmountMinor,
                    storeOrder.Items.Single().CommissionPercent,
                    true,
                    ct
                );
        }
        var order = storeOrder.OrderId.HasValue
            ? await InPersonOrderMapping.GetOrderAsync(storeOrder.OrderId.Value, mediator, ct)
            : await StartInPersonOrderHandler.ResumeOrderAsync(
                storeOrder,
                financial!,
                mediator,
                storeOrders,
                ct
            );
        logger.LogInformation(
            "In-person StoreOrder ready. Initiator={Initiator} SupplierId={SupplierId} ShopId={ShopId} ProductId={ProductId} StoreOrderId={StoreOrderId} OrderId={OrderId} IdempotencyKey={IdempotencyKey}",
            "User",
            storeOrder.SupplierId,
            storeOrder.ShopId,
            storeOrder.Items.Single().ProductId,
            storeOrder.Id,
            order.Id,
            storeOrder.IdempotencyKey
        );
        return InPersonOrderMapping.ToDto(storeOrder, order);
    }
}

public sealed class VerifyInPersonOrderHandler(
    INotificationService notification,
    IInPersonOtpReferenceProtector otpReferences,
    IStoreOrderRepository storeOrders,
    IMediator mediator,
    Microsoft.Extensions.Logging.ILogger<VerifyInPersonOrderHandler> logger
) : IRequestHandler<VerifyInPersonOrderCommand, InPersonOrderDto>
{
    public async Task<InPersonOrderDto> Handle(
        VerifyInPersonOrderCommand request,
        CancellationToken ct
    )
    {
        var order = await InPersonOrderMapping.GetOrderAsync(request.OrderId, mediator, ct);
        var storeOrder =
            await storeOrders.GetByOrderIdAsync(order.Id, ct)
            ?? throw new StoreDomainException("سفارش فروشگاه یافت نشد", "STORE_ORDER_NOT_FOUND");
        EnsureVendorOrder(storeOrder);
        await EnsurePermission(
            request.VendorUserId,
            storeOrder,
            StorePermissions.CreateInPersonOrder,
            mediator,
            ct
        );
        var user = (
            await mediator.Send(new GetOrderUserSummariesQuery([order.UserId]), ct)
        ).Single();
        var mobile = InPersonOrderMapping.NormalizeMobile(user.MobileNumber ?? string.Empty);
        if (order.PaymentState == "Paid")
            return InPersonOrderMapping.ToDto(storeOrder, order, mobile);

        if (!storeOrder.OtpVerifiedAt.HasValue)
        {
            if (
                !string.Equals(
                    request.OtpReferenceCode,
                    storeOrder.OtpReferenceCode,
                    StringComparison.Ordinal
                )
                || !otpReferences.TryUnprotect(request.OtpReferenceCode, out var reference)
                || reference is null
                || reference.OrderId != order.Id
                || reference.ShopId != storeOrder.ShopId
                || !string.Equals(
                    InPersonOrderMapping.NormalizeMobile(reference.MobileNumber),
                    mobile,
                    StringComparison.Ordinal
                )
            )
                throw new StoreDomainException(
                    "مرجع کد تایید متعلق به این سفارش نیست",
                    "OTP_REFERENCE_MISMATCH"
                );
            var validation = await notification.ValidateOtp(
                reference.ProviderReferenceCode,
                request.OtpCode,
                OtpType.VendorInPersonPayment,
                ct
            );
            if (
                !validation.IsValid
                || (
                    !string.IsNullOrWhiteSpace(validation.Receipt)
                    && InPersonOrderMapping.NormalizeMobile(validation.Receipt) != mobile
                )
            )
                throw new StoreDomainException(
                    "کد تایید نامعتبر یا منقضی است",
                    "OTP_INVALID_OR_EXPIRED"
                );
            storeOrder.MarkOtpVerified();
            await storeOrders.UpdateAsync(storeOrder, ct);
        }

        var options =
            await mediator.Send(
                new GetOrderPaymentOptionsQuery(order.Id, order.UserId, "Admin"),
                ct
            )
            ?? throw new StoreDomainException(
                "گزینه‌های پرداخت در دسترس نیست",
                "PAYMENT_OPTIONS_UNAVAILABLE"
            );
        if (!options.IsCovered)
            throw new StoreDomainException(
                "موجودی کیف پول‌های مجاز کافی نیست",
                "PAYMENT_BALANCE_INSUFFICIENT"
            );
        await mediator.Send(
            new PayOrderCommand(
                order.Id,
                order.UserId,
                "User",
                options
                    .Allocations.Select(x => new WalletAllocationInput(x.WalletId, x.AmountMinor))
                    .ToList(),
                $"store-in-person-pay:{order.Id:N}"
            ),
            ct
        );
        order = await InPersonOrderMapping.GetOrderAsync(order.Id, mediator, ct);
        logger.LogInformation(
            "Vendor in-person Order payment completed or resumed. SupplierId={SupplierId} ShopId={ShopId} StoreOrderId={StoreOrderId} OrderId={OrderId}",
            storeOrder.SupplierId,
            storeOrder.ShopId,
            storeOrder.Id,
            order.Id
        );
        return InPersonOrderMapping.ToDto(storeOrder, order, mobile);
    }

    internal static void EnsureVendorOrder(StoreOrder order)
    {
        if (order.SalesChannel != SalesChannel.InPerson || order.InitiatorType != "Vendor")
            throw new StoreDomainException(
                "سفارش از نوع فروش حضوری Vendor نیست",
                "NOT_VENDOR_IN_PERSON_ORDER"
            );
    }

    internal static async Task EnsurePermission(
        Guid actor,
        StoreOrder order,
        string permission,
        IMediator mediator,
        CancellationToken ct
    )
    {
        if (
            !await mediator.Send(
                new AuthorizeStoreResourceQuery(actor, order.SupplierId, order.ShopId, permission),
                ct
            )
        )
            throw new UnauthorizedAccessException("دسترسی به این فروش وجود ندارد");
    }
}

public sealed class ResendInPersonOrderOtpHandler(
    INotificationService notification,
    IInPersonOtpReferenceProtector otpReferences,
    IStoreOrderRepository storeOrders,
    IMediator mediator
) : IRequestHandler<ResendInPersonOrderOtpCommand, InPersonOrderDto>
{
    public async Task<InPersonOrderDto> Handle(
        ResendInPersonOrderOtpCommand request,
        CancellationToken ct
    )
    {
        var order = await InPersonOrderMapping.GetOrderAsync(request.OrderId, mediator, ct);
        var storeOrder =
            await storeOrders.GetByOrderIdAsync(order.Id, ct)
            ?? throw new StoreDomainException("سفارش فروشگاه یافت نشد", "STORE_ORDER_NOT_FOUND");
        VerifyInPersonOrderHandler.EnsureVendorOrder(storeOrder);
        if (order.PaymentState == "Paid")
            throw new StoreDomainException("سفارش قبلاً پرداخت شده است", "ORDER_ALREADY_PAID");
        if (storeOrder.OtpVerifiedAt.HasValue)
            throw new StoreDomainException("کد تایید قبلاً تایید شده است", "OTP_ALREADY_VERIFIED");
        await VerifyInPersonOrderHandler.EnsurePermission(
            request.VendorUserId,
            storeOrder,
            StorePermissions.CreateInPersonOrder,
            mediator,
            ct
        );
        var user = (
            await mediator.Send(new GetOrderUserSummariesQuery([order.UserId]), ct)
        ).Single();
        var mobile = InPersonOrderMapping.NormalizeMobile(user.MobileNumber ?? string.Empty);
        var challenge = await notification.SendOtp(
            mobile,
            OtpReceiptType.Sms,
            OtpType.VendorInPersonPayment,
            ct
        );
        storeOrder.AttachOtpChallenge(
            otpReferences.Protect(
                new(order.Id, storeOrder.ShopId, mobile, challenge.ReferenceCode)
            ),
            challenge.ExpiresAt
        );
        await storeOrders.UpdateAsync(storeOrder, ct);
        return InPersonOrderMapping.ToDto(storeOrder, order, mobile);
    }
}

public sealed class CancelInPersonOrderHandler(
    IStoreOrderRepository storeOrders,
    IMediator mediator
) : IRequestHandler<CancelInPersonOrderCommand, InPersonOrderDto>
{
    public async Task<InPersonOrderDto> Handle(
        CancelInPersonOrderCommand request,
        CancellationToken ct
    )
    {
        var order = await InPersonOrderMapping.GetOrderAsync(request.OrderId, mediator, ct);
        var storeOrder =
            await storeOrders.GetByOrderIdAsync(order.Id, ct)
            ?? throw new StoreDomainException("سفارش فروشگاه یافت نشد", "STORE_ORDER_NOT_FOUND");
        VerifyInPersonOrderHandler.EnsureVendorOrder(storeOrder);
        await VerifyInPersonOrderHandler.EnsurePermission(
            request.VendorUserId,
            storeOrder,
            order.PaymentState == "Paid"
                ? StorePermissions.RefundInPersonOrder
                : StorePermissions.CreateInPersonOrder,
            mediator,
            ct
        );
        await mediator.Send(
            new CancelOrderCommand(
                order.Id,
                "لغو فروش حضوری",
                $"store-in-person-cancel:{order.Id:N}:{request.IdempotencyKey.Trim()}"
            ),
            ct
        );
        return InPersonOrderMapping.ToDto(
            storeOrder,
            await InPersonOrderMapping.GetOrderAsync(order.Id, mediator, ct)
        );
    }
}
