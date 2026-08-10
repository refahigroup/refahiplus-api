using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Features.Cart.AddToCart;

public class AddToCartCommandHandler : IRequestHandler<AddToCartCommand, AddToCartResponse>
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IProductSessionRepository _sessionRepo;
    private readonly IStoreProductPriceResolver _priceResolver;
    private readonly IMediator _mediator;

    public AddToCartCommandHandler(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IProductSessionRepository sessionRepo,
        IStoreProductPriceResolver priceResolver,
        IMediator mediator
    )
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _sessionRepo = sessionRepo;
        _priceResolver = priceResolver;
        _mediator = mediator;
    }

    public async Task<AddToCartResponse> Handle(
        AddToCartCommand request,
        CancellationToken cancellationToken
    )
    {
        var product =
            await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        if (product.IsDeleted)
            throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        // Get sales model from AgreementProduct; price comes from ShopProduct
        var ap =
            await _mediator.Send(
                new GetAgreementProductByIdQuery(product.AgreementProductId),
                cancellationToken
            )
            ?? throw new StoreDomainException(
                "اطلاعات محصول یافت نشد",
                "AGREEMENT_PRODUCT_NOT_FOUND"
            );

        var salesModel = (SalesModel)ap.SalesModel;
        var isManual = ap.PricingMode == (short)PricingMode.Manual;
        if (isManual)
        {
            if (
                request.Quantity != 1
                || request.VariantId.HasValue
                || request.SessionId.HasValue
                || request.UsageDate.HasValue
            )
                throw new StoreDomainException(
                    "محصول حضوری فقط با تعداد یک و بدون تنوع یا سانس قابل خرید است",
                    "INVALID_MANUAL_PRODUCT_SELECTION"
                );
            if (!request.ManualAmountMinor.HasValue || request.ManualAmountMinor.Value <= 0)
                throw new StoreDomainException(
                    "مبلغ خرید حضوری الزامی است",
                    "MANUAL_AMOUNT_REQUIRED"
                );
        }
        else if (request.ManualAmountMinor.HasValue)
        {
            throw new StoreDomainException(
                "مبلغ دستی برای این محصول مجاز نیست",
                "MANUAL_AMOUNT_NOT_ALLOWED"
            );
        }

        var currentCart = await _cartRepo.GetByUserAndModuleIdAsync(
            request.UserId,
            request.ModuleId,
            cancellationToken
        );
        if (currentCart is { Items.Count: > 0 })
        {
            var sameManualItem =
                isManual
                && currentCart.Items.Count == 1
                && currentCart.Items[0].ShopId == request.ShopId
                && currentCart.Items[0].ProductId == request.ProductId;
            if (isManual && !sameManualItem)
                throw new StoreDomainException(
                    "سبد خرید حضوری فقط می‌تواند شامل یک محصول باشد",
                    "MANUAL_CART_MUST_BE_SINGLE_ITEM"
                );

            if (!isManual)
            {
                foreach (var item in currentCart.Items)
                {
                    var existingProduct = await _productRepo.GetByIdAsync(
                        item.ProductId,
                        cancellationToken
                    );
                    if (existingProduct is null)
                        continue;
                    var existingAp = await _mediator.Send(
                        new GetAgreementProductByIdQuery(existingProduct.AgreementProductId),
                        cancellationToken
                    );
                    if (existingAp?.PricingMode == (short)PricingMode.Manual)
                        throw new StoreDomainException(
                            "محصول دیگری را نمی‌توان به سبد خرید حضوری اضافه کرد",
                            "MANUAL_CART_MUST_BE_SINGLE_ITEM"
                        );
                }
            }
        }

        var priceVariantId =
            salesModel == SalesModel.SessionBased && request.SessionId.HasValue
                ? null
                : request.VariantId;
        var resolvedPrice = isManual
            ? null
            : await _priceResolver.ResolveAsync(
                request.ShopId,
                product,
                priceVariantId,
                cancellationToken
            );
        long unitPrice = isManual
            ? request.ManualAmountMinor!.Value
            : resolvedPrice!.UnitPriceMinor;
        var normalizedUsageDate = request.UsageDate;

        if (salesModel == SalesModel.Unlimited)
        {
            normalizedUsageDate = null;
        }
        else if (salesModel == SalesModel.StockBased)
        {
            normalizedUsageDate = null;

            if (request.VariantId.HasValue)
            {
                var variant =
                    product.Variants.FirstOrDefault(v => v.Id == request.VariantId.Value)
                    ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");

                if (!variant.IsAvailable)
                    throw new StoreDomainException(
                        "این تنوع محصول موجود نیست",
                        "VARIANT_NOT_AVAILABLE"
                    );

                if (!variant.HasLegacyStockAvailable(request.Quantity))
                    throw new StoreDomainException("موجودی کافی نیست", "INSUFFICIENT_STOCK");
            }
            else
            {
                if (product.StockCount < request.Quantity)
                    throw new StoreDomainException("موجودی کافی نیست", "INSUFFICIENT_STOCK");
            }
        }
        else // SessionBased
        {
            if (request.SessionId.HasValue)
            {
                normalizedUsageDate = null;

                var session =
                    product.Sessions.FirstOrDefault(s => s.Id == request.SessionId.Value)
                    ?? throw new StoreDomainException("سانس یافت نشد", "SESSION_NOT_FOUND");

                if (!session.IsAvailable)
                    throw new StoreDomainException(
                        "این سانس در دسترس نیست",
                        "SESSION_NOT_AVAILABLE"
                    );

                if (session.RemainingCapacity < request.Quantity)
                    throw new StoreDomainException("ظرفیت کافی نیست", "INSUFFICIENT_CAPACITY");

                unitPrice += session.PriceAdjustment;
            }
            else if (request.VariantId.HasValue)
            {
                var variant =
                    product.Variants.FirstOrDefault(v => v.Id == request.VariantId.Value)
                    ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");

                normalizedUsageDate = StoreVariantCapacityService.NormalizeAndValidateUsageDate(
                    variant,
                    request.UsageDate
                );
                await StoreVariantCapacityService.EnsureCapacityAvailableAsync(
                    variant,
                    normalizedUsageDate,
                    request.Quantity,
                    _mediator,
                    excludeOrderId: null,
                    cancellationToken
                );
            }
            else
            {
                throw new StoreDomainException(
                    "برای محصولات سانسی، انتخاب سانس یا خدمت الزامی است",
                    "SESSION_REQUIRED"
                );
            }
        }

        var cart = await (
            isManual
                ? _cartRepo.ReplaceItemAsync(
                    request.UserId,
                    request.ModuleId,
                    request.ShopId,
                    request.ProductId,
                    request.VariantId,
                    request.SessionId,
                    normalizedUsageDate,
                    request.Quantity,
                    unitPrice,
                    cancellationToken
                )
                : _cartRepo.AddItemAsync(
                    request.UserId,
                    request.ModuleId,
                    request.ShopId,
                    request.ProductId,
                    request.VariantId,
                    request.SessionId,
                    normalizedUsageDate,
                    request.Quantity,
                    unitPrice,
                    cancellationToken
                )
        );

        return new AddToCartResponse(cart.Id, cart.Items.Sum(i => i.Quantity));
    }
}
