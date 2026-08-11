using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Refahi.Modules.Store.Api.Endpoints.CatalogV3;
using Refahi.Modules.Store.Api.Security;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Application.Contracts.Commands.Sessions;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Offers;
using Refahi.Modules.Store.Application.Contracts.Products.V3;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Application.Features.CatalogV3;
using Refahi.Modules.Store.Application.Features.Products.AddProductImage;
using Refahi.Modules.Store.Application.Features.Products.AddProductSpecification;
using Refahi.Modules.Store.Application.Features.Products.AddProductVariant;
using Refahi.Modules.Store.Application.Features.Products.AdminGetProduct;
using Refahi.Modules.Store.Application.Features.Products.RemoveProductImage;
using Refahi.Modules.Store.Application.Features.Products.UpdateProductVariant;
using Refahi.Modules.Store.Application.Features.Sessions.CreateSession;
using Refahi.Modules.Store.Application.Features.VendorAccess;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementCategoryTerms;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Agreements;
using Refahi.Shared.Services.Path;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class CatalogV3PaginationEligibilityTests
{
    [Theory]
    [InlineData((short)SalesChannel.Online)]
    [InlineData((short)SalesChannel.InPerson)]
    public async Task Create_product_returns_computed_current_eligible_channels(
        short availableChannel
    )
    {
        var repository = new FakeProductRepository([]);
        var mediator = new EligibilityMediator(new HashSet<int> { 7 }, availableChannel);
        var command = new CreateProductV3Command(
            Guid.NewGuid(),
            true,
            Guid.NewGuid(),
            7,
            0,
            (short)ProductType.Goods,
            (short)SalesModel.Unlimited,
            (short)FulfillmentMethod.Shipping,
            "محصول",
            $"product-{availableChannel}",
            null
        );

        var result = await new CreateProductV3Handler(repository, mediator).Handle(
            command,
            CancellationToken.None
        );

        Assert.Equal(availableChannel, result.EligibleSalesChannels);
        Assert.Equal(Guid.Empty, repository.AddedProduct!.AgreementProductId);
        Assert.Equal(0, mediator.SingleCalls);
    }

    [Theory]
    [InlineData((short)SalesChannel.Online)]
    [InlineData((short)SalesChannel.InPerson)]
    public async Task Activation_uses_any_current_valid_channel_without_client_guess(
        short availableChannel
    )
    {
        var product = Product.CreateCatalogProduct(
            Guid.NewGuid(),
            7,
            ProductType.Goods,
            SalesModel.Unlimited,
            FulfillmentMethod.Shipping,
            "محصول",
            $"activation-{availableChannel}"
        );
        product.Suspend();
        var repository = new FakeProductRepository([product]);
        var mediator = new EligibilityMediator(new HashSet<int> { 7 }, availableChannel);
        var deliberatelyWrongClientChannel =
            availableChannel == (short)SalesChannel.Online
                ? (short)SalesChannel.InPerson
                : (short)SalesChannel.Online;

        var result = await new SetProductV3ActivationHandler(repository, mediator).Handle(
            new SetProductV3ActivationCommand(
                Guid.NewGuid(),
                true,
                product.Id,
                deliberatelyWrongClientChannel,
                true
            ),
            CancellationToken.None
        );

        Assert.True(result.IsActive);
        Assert.Equal(availableChannel, result.EligibleSalesChannels);
        Assert.Equal(0, mediator.SingleCalls);
    }

    [Fact]
    public async Task V3_product_can_create_and_update_variant_without_agreement_product_authority()
    {
        var product = Product.CreateCatalogProduct(
            Guid.NewGuid(),
            7,
            ProductType.Goods,
            SalesModel.InventoryBased,
            FulfillmentMethod.Shipping,
            "محصول",
            "variant-product"
        );
        var attribute = product.AddVariantAttribute("رنگ");
        var value = product.AddVariantAttributeValue(attribute.Id, "قرمز");
        var repository = new FakeProductRepository([product]);
        var mediator = new EligibilityMediator(new HashSet<int>());
        var combinations = new List<VariantCombinationInput> { new(attribute.Id, value.Id) };

        var created = await new AddProductVariantCommandHandler(repository, mediator).Handle(
            new AddProductVariantCommand(product.Id, combinations, null, 2, 10_000, 9_000, "SKU-1"),
            CancellationToken.None
        );
        await new UpdateProductVariantCommandHandler(repository, mediator).Handle(
            new UpdateProductVariantCommand(
                product.Id,
                created.VariantId,
                combinations,
                null,
                4,
                12_000,
                11_000,
                "SKU-2"
            ),
            CancellationToken.None
        );

        var variant = Assert.Single(product.Variants);
        Assert.Equal(SalesModel.InventoryBased, product.SalesModel);
        Assert.Equal(4, variant.StockCount);
        Assert.Equal("SKU-2", variant.SKU);
        Assert.Equal(0, mediator.SingleCalls);
    }

    [Fact]
    public async Task Legacy_variant_creation_still_uses_agreement_product_sales_model()
    {
        var agreementProductId = Guid.NewGuid();
        var product = Product.Create(agreementProductId, "قدیمی", "legacy", stockCount: 1);
        var attribute = product.AddVariantAttribute("رنگ");
        var value = product.AddVariantAttributeValue(attribute.Id, "آبی");
        var repository = new FakeProductRepository([product]);
        var mediator = new LegacyAgreementMediator(agreementProductId);

        await new AddProductVariantCommandHandler(repository, mediator).Handle(
            new AddProductVariantCommand(
                product.Id,
                [new VariantCombinationInput(attribute.Id, value.Id)],
                null,
                3,
                1000,
                900,
                null
            ),
            CancellationToken.None
        );

        Assert.Equal(1, mediator.AgreementCalls);
        var variant = Assert.Single(product.Variants);
        Assert.True(variant.IsAvailable);
        Assert.Equal(1000, variant.PriceMinor);
        Assert.Equal(900, variant.DiscountedPriceMinor);
    }

    [Fact]
    public void V3_variant_and_session_contracts_do_not_accept_or_expose_parallel_prices()
    {
        var contractTypes = new[]
        {
            typeof(ProductVariantStructuralV3Request),
            typeof(CreateProductVariantV3Command),
            typeof(UpdateProductVariantV3Command),
            typeof(ProductVariantV3Dto),
            typeof(ProductSessionCreateV3Request),
            typeof(ProductSessionUpdateV3Request),
            typeof(CreateProductSessionV3Command),
            typeof(UpdateProductSessionV3Command),
            typeof(ProductSessionV3Dto),
            typeof(AddProductImageCommand),
            typeof(AddProductSpecificationCommand),
        };

        var forbidden = contractTypes.SelectMany(type =>
            type.GetProperties()
                .Where(property =>
                    property.Name.Contains("Price", StringComparison.OrdinalIgnoreCase)
                )
                .Select(property => $"{type.Name}.{property.Name}")
        );

        Assert.Empty(forbidden);
    }

    [Fact]
    public async Task V3_variant_writes_persist_neutral_legacy_prices_and_leave_offer_unchanged()
    {
        var product = Product.CreateCatalogProduct(
            Guid.NewGuid(),
            12,
            ProductType.Goods,
            SalesModel.InventoryBased,
            FulfillmentMethod.Shipping,
            "محصول",
            "structural-variant"
        );
        var attribute = product.AddVariantAttribute("رنگ");
        var value = product.AddVariantAttributeValue(attribute.Id, "سبز");
        var repository = new FakeProductRepository([product]);
        var actorId = Guid.NewGuid();
        var authorization = new FilterMediator(product.SupplierId);
        var pathService = new FakePathService();
        var handlers = new ProductV3SubresourceHandlers(repository, authorization, pathService);
        var offer = Offer.Create(
            product.Id,
            Guid.NewGuid(),
            null,
            null,
            75_000,
            12.50m,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            null
        );
        var offerSnapshot = (
            offer.OriginalPriceMinor,
            offer.DiscountPercent,
            offer.FinalPriceMinor,
            offer.Version
        );

        var created = await handlers.Handle(
            new CreateProductVariantV3Command(
                actorId,
                false,
                product.Id,
                [new VariantCombinationV3Input(attribute.Id, value.Id)],
                null,
                4,
                "V3-1"
            ),
            CancellationToken.None
        );
        var stored = Assert.Single(product.Variants);
        Assert.Equal(1, stored.PriceMinor);
        Assert.Equal(1, stored.DiscountedPriceMinor);
        Assert.Equal(4, created.StockCount);

        var updated = await handlers.Handle(
            new UpdateProductVariantV3Command(
                actorId,
                false,
                product.Id,
                stored.Id,
                [new VariantCombinationV3Input(attribute.Id, value.Id)],
                null,
                7,
                "V3-2"
            ),
            CancellationToken.None
        );
        Assert.Equal(7, updated.StockCount);
        Assert.Equal(1, stored.PriceMinor);
        Assert.Equal(1, stored.DiscountedPriceMinor);
        Assert.Equal(
            offerSnapshot,
            (offer.OriginalPriceMinor, offer.DiscountPercent, offer.FinalPriceMinor, offer.Version)
        );
        Assert.Equal(StorePermissions.ManageCatalog, authorization.LastPermission);

        await handlers.Handle(
            new DeleteProductVariantV3Command(actorId, false, product.Id, stored.Id),
            CancellationToken.None
        );
        Assert.Empty(product.Variants);

        var deniedHandlers = new ProductV3SubresourceHandlers(repository,new FilterMediator(Guid.NewGuid()), pathService);
        var denied =
            await Assert.ThrowsAsync<Refahi.Modules.Store.Domain.Exceptions.StoreDomainException>(
                () =>
                    deniedHandlers.Handle(
                        new CreateProductVariantV3Command(
                            actorId,
                            false,
                            product.Id,
                            [new VariantCombinationV3Input(attribute.Id, value.Id)],
                            null,
                            1,
                            null
                        ),
                        CancellationToken.None
                    )
            );
        Assert.Equal("CATALOG_ACCESS_DENIED", denied.ErrorCode);
    }

    [Fact]
    public async Task V3_session_writes_neutralize_legacy_price_adjustment()
    {
        var product = Product.CreateCatalogProduct(
            Guid.NewGuid(),
            13,
            ProductType.Service,
            SalesModel.SessionBased,
            FulfillmentMethod.Voucher,
            "خدمت",
            "structural-session"
        );

        var pathService = new FakePathService();

        var handlers = new ProductV3SubresourceHandlers(
            new FakeProductRepository([product]),
            new EligibilityMediator(new HashSet<int>()),
            pathService
        );

        var created = await handlers.Handle(
            new CreateProductSessionV3Command(
                Guid.NewGuid(),
                true,
                product.Id,
                "2026-08-12",
                "09:00",
                "10:00",
                8,
                "صبح"
            ),
            CancellationToken.None
        );
        var stored = Assert.Single(product.Sessions);
        Assert.Equal(0, stored.PriceAdjustment);

        var updated = await handlers.Handle(
            new UpdateProductSessionV3Command(
                Guid.NewGuid(),
                true,
                product.Id,
                created.Id,
                10,
                "صبح جدید",
                false
            ),
            CancellationToken.None
        );
        Assert.Equal(10, updated.Capacity);
        Assert.False(updated.IsActive);
        Assert.Equal(0, stored.PriceAdjustment);
    }

    [Fact]
    public async Task V3_detail_includes_session_specification_image_and_uses_offer_pricing_mode()
    {
        var product = Product.CreateCatalogProduct(
            Guid.NewGuid(),
            9,
            ProductType.Service,
            SalesModel.SessionBased,
            FulfillmentMethod.Voucher,
            "خدمت",
            "session-product"
        );
        var repository = new FakeProductRepository([product]);
        var mediator = new EligibilityMediator(new HashSet<int>());

        await new AddProductImageCommandHandler(repository).Handle(
            new AddProductImageCommand(product.Id, "/media/p.png", true, 0),
            CancellationToken.None
        );
        await new AddProductSpecificationCommandHandler(repository).Handle(
            new AddProductSpecificationCommand(product.Id, "مدت", "یک ساعت", 0),
            CancellationToken.None
        );
        await new CreateSessionCommandHandler(repository, mediator).Handle(
            new CreateSessionCommand(product.Id, "2026-08-10", "10:00", "11:00", 5, "صبح", 0),
            CancellationToken.None
        );

        var detail = await new AdminGetProductQueryHandler(
            repository,
            new StubShopProductRepository(),
            new StubReviewRepository(),
            mediator,
            new IdentityPathService()
        ).Handle(new AdminGetProductQuery(product.Id), CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Offer", detail.PricingMode);
        Assert.Equal(nameof(ProductType.Service), detail.ProductType);
        Assert.Equal(nameof(FulfillmentMethod.Voucher), detail.DeliveryType);
        Assert.Equal(nameof(SalesModel.SessionBased), detail.SalesModel);
        Assert.Equal(9, detail.CategoryId);
        Assert.Single(detail.Images);
        Assert.Single(detail.Specifications);
        Assert.Single(detail.Sessions!);
        Assert.Equal(0, detail.PriceMinor);
        Assert.Equal(0, mediator.SingleCalls);

        var productDto = new ProductV3Dto(
            product.Id,
            product.SupplierId,
            product.CategoryId,
            (short)product.ProductType,
            (short)product.SalesModel,
            (short)product.FulfillmentMethod,
            product.Title,
            product.Slug,
            product.Description,
            product.IsAvailable,
            product.IsDeleted,
            product.CreatedAt,
            product.UpdatedAt,
            product.Version
        )
        {
            EligibleSalesChannels = 3,
        };
        var vendorDetail = await new GetProductV3ManagementDetailHandler(
            new DetailCompositionMediator(productDto, detail)
        ).Handle(new GetProductV3ManagementDetailQuery(product.Id), CancellationToken.None);
        Assert.NotNull(vendorDetail);
        Assert.Equal("Offer", vendorDetail.PricingAuthority);
        Assert.Equal(
            $"/api/store/v3/vendor/products/{product.Id}",
            vendorDetail.SubresourceBasePath
        );
        Assert.Single(vendorDetail.Images);
        Assert.Single(vendorDetail.Specifications);
        Assert.Single(vendorDetail.Sessions);

        await new RemoveProductImageCommandHandler(repository).Handle(
            new RemoveProductImageCommand(product.Id, product.Images.Single().Id),
            CancellationToken.None
        );
        Assert.Empty(product.Images);
    }

    [Theory]
    [InlineData("POST", "/api/store/provider/products/item/variants")]
    [InlineData("PUT", "/api/store/provider/products/item/variants/variant")]
    [InlineData("DELETE", "/api/store/provider/products/item/images/1")]
    public async Task Provider_filter_authorizes_v3_subresources_by_product_supplier_without_legacy_lookup(
        string method,
        string path
    )
    {
        var supplierId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var product = Product.CreateCatalogProduct(
            supplierId,
            1,
            ProductType.Goods,
            SalesModel.InventoryBased,
            FulfillmentMethod.Shipping,
            "محصول",
            "owned"
        );
        var repository = new FakeProductRepository([product]);
        var mediator = new FilterMediator(supplierId);
        var filter = new StoreProviderOwnershipFilter(
            new StubShopRepository(),
            repository,
            new StubProductSessionRepository(),
            mediator
        );
        var http = ProviderHttp(userId, method, path, product.Id);
        var invocation = new DefaultEndpointFilterInvocationContext(http, Array.Empty<object?>());
        var marker = new object();

        var result = await filter.InvokeAsync(
            invocation,
            _ => ValueTask.FromResult<object?>(marker)
        );

        Assert.Same(marker, result);
        Assert.Equal(StorePermissions.ManageCatalog, mediator.LastPermission);
        Assert.Equal(0, mediator.AgreementLookups);
    }

    [Fact]
    public async Task Provider_filter_denies_non_owner_and_legacy_product_still_falls_back_to_agreement()
    {
        var userId = Guid.NewGuid();
        var v3 = Product.CreateCatalogProduct(
            Guid.NewGuid(),
            1,
            ProductType.Goods,
            SalesModel.Unlimited,
            FulfillmentMethod.Shipping,
            "جدید",
            "new-owned"
        );
        var deniedMediator = new FilterMediator(Guid.NewGuid());
        var deniedFilter = new StoreProviderOwnershipFilter(
            new StubShopRepository(),
            new FakeProductRepository([v3]),
            new StubProductSessionRepository(),
            deniedMediator
        );
        var deniedHttp = ProviderHttp(
            userId,
            "POST",
            "/api/store/provider/products/item/images",
            v3.Id
        );
        var denied = await deniedFilter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(deniedHttp, Array.Empty<object?>()),
            _ => ValueTask.FromResult<object?>(new object())
        );
        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(denied).StatusCode
        );
        Assert.Equal(0, deniedMediator.AgreementLookups);

        var supplierId = Guid.NewGuid();
        var agreementProductId = Guid.NewGuid();
        var legacy = Product.Create(agreementProductId, "قدیمی", "legacy-owned", stockCount: 1);
        var legacyMediator = new FilterMediator(supplierId, agreementProductId);
        var legacyFilter = new StoreProviderOwnershipFilter(
            new StubShopRepository(),
            new FakeProductRepository([legacy]),
            new StubProductSessionRepository(),
            legacyMediator
        );
        var legacyHttp = ProviderHttp(
            userId,
            "POST",
            "/api/store/provider/products/item/variants",
            legacy.Id
        );
        var marker = new object();
        var allowed = await legacyFilter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(legacyHttp, Array.Empty<object?>()),
            _ => ValueTask.FromResult<object?>(marker)
        );
        Assert.Same(marker, allowed);
        Assert.Equal(1, legacyMediator.AgreementLookups);
    }

    [Fact]
    public void Vendor_capabilities_distinguish_supplier_product_scope_from_shop_offer_scope()
    {
        var vendorId = Guid.NewGuid();
        var shopId = Guid.NewGuid();
        var supplierUser = new StoreVendorContextDto(
            vendorId,
            "تامین‌کننده",
            [],
            [StorePermissions.ManageCatalog],
            []
        );
        var shopSupervisor = new StoreVendorContextDto(
            vendorId,
            "تامین‌کننده",
            [],
            [],
            [
                new VendorShopAccessDto(
                    shopId,
                    "شعبه",
                    "Active",
                    "InPerson",
                    null,
                    [StoreAccessRoles.ShopSupervisor],
                    [StorePermissions.ManageCatalog]
                ),
            ]
        );

        Assert.True(supplierUser.CanManageProducts);
        Assert.False(shopSupervisor.CanManageProducts);
        Assert.Empty(supplierUser.ManageOfferShopIds);
        Assert.Equal([shopId], shopSupervisor.ManageOfferShopIds);
    }

    [Fact]
    public async Task Shop_supervisor_is_denied_product_scope_but_allowed_offer_for_assigned_shop()
    {
        var userId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var shopId = Guid.NewGuid();
        var context = new StoreVendorContextDto(
            vendorId,
            "تامین‌کننده",
            [],
            [],
            [
                new VendorShopAccessDto(
                    shopId,
                    "شعبه",
                    "Active",
                    "InPerson",
                    null,
                    [StoreAccessRoles.ShopSupervisor],
                    [StorePermissions.ManageCatalog]
                ),
            ]
        );
        var handler = new AuthorizeStoreResourceHandler(new VendorContextMediator(userId, context));

        Assert.False(
            await handler.Handle(
                new AuthorizeStoreResourceQuery(
                    userId,
                    vendorId,
                    null,
                    StorePermissions.ManageCatalog
                ),
                CancellationToken.None
            )
        );
        Assert.True(
            await handler.Handle(
                new AuthorizeStoreResourceQuery(
                    userId,
                    vendorId,
                    shopId,
                    StorePermissions.ManageCatalog
                ),
                CancellationToken.None
            )
        );
        Assert.False(
            await handler.Handle(
                new AuthorizeStoreResourceQuery(
                    userId,
                    vendorId,
                    Guid.NewGuid(),
                    StorePermissions.ManageCatalog
                ),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task Public_v3_catalog_scopes_module_shop_and_eligibility_before_deduplicated_pagination()
    {
        var supplierA = Guid.NewGuid();
        var supplierB = Guid.NewGuid();
        var shopA = Guid.NewGuid();
        var shopB = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var productC = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var candidates = new List<PublicCatalogOfferCandidate>
        {
            PublicCandidate(
                productA,
                supplierA,
                shopA,
                "shop-a",
                "کفش یک",
                7,
                20_000,
                now.AddDays(1)
            ),
            PublicCandidate(
                productA,
                supplierA,
                shopB,
                "shop-b",
                "کفش یک",
                7,
                18_000,
                now.AddDays(1)
            ),
            PublicCandidate(
                productB,
                supplierA,
                shopA,
                "shop-a",
                "کفش دو",
                7,
                30_000,
                now.AddDays(1)
            ),
            PublicCandidate(
                productC,
                supplierB,
                shopA,
                "shop-a",
                "کفش سه",
                7,
                10_000,
                now.AddDays(1)
            ),
            PublicCandidate(
                Guid.NewGuid(),
                supplierA,
                shopA,
                "shop-a",
                "منقضی",
                7,
                1_000,
                now.AddMinutes(-1)
            ),
            PublicCandidate(
                Guid.NewGuid(),
                supplierA,
                shopA,
                "shop-a",
                "دسته دیگر",
                8,
                2_000,
                now.AddDays(1)
            ),
        };

        var catalog = new FakePublicCatalogRepository(candidates);
        var eligibility = new PublicEligibilityMediator(new HashSet<Guid> { supplierA });
        var pathService = new FakePathService();

        var handler = new GetPublicProductCatalogV3Handler(
            catalog,
            new FakeStoreModuleRepository(StoreModule.Create("فروشگاه", "store", categoryId: 7)),
            eligibility,
            pathService
        );

        var first = await handler.Handle(
            new GetPublicProductCatalogV3Query(
                "store",
                "کفش",
                7,
                null,
                null,
                null,
                null,
                null,
                "price-asc",
                1,
                1
            ),
            CancellationToken.None
        );
        var second = await handler.Handle(
            new GetPublicProductCatalogV3Query(
                "store",
                "کفش",
                7,
                null,
                null,
                null,
                null,
                null,
                "price-asc",
                2,
                1
            ),
            CancellationToken.None
        );
        var shopScoped = await handler.Handle(
            new GetPublicProductCatalogV3Query(
                "store",
                null,
                null,
                null,
                "shop-b",
                null,
                null,
                null,
                "newest",
                1,
                30
            ),
            CancellationToken.None
        );
        var priceAndModelFiltered = await handler.Handle(
            new GetPublicProductCatalogV3Query(
                "store",
                null,
                null,
                shopA,
                null,
                "StockBased",
                25_000,
                35_000,
                "price-desc",
                1,
                30
            ),
            CancellationToken.None
        );

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(shopScoped);
        Assert.NotNull(priceAndModelFiltered);
        Assert.Equal(2, first.Total);
        Assert.Equal(productA, Assert.Single(first.Items).ProductId);
        Assert.Equal(2, first.Items[0].Price.OfferCount);
        Assert.Equal(18_000, first.Items[0].Price.MinFinalPriceMinor);
        Assert.Equal(productB, Assert.Single(second.Items).ProductId);
        Assert.Equal(productA, Assert.Single(shopScoped.Items).ProductId);
        Assert.Equal("shop-b", shopScoped.Items[0].Price.DefaultShopSlug);
        Assert.Equal(productB, Assert.Single(priceAndModelFiltered.Items).ProductId);
        Assert.Equal(4, eligibility.BatchCalls);
        Assert.Equal(0, eligibility.SingleCalls);
        Assert.All(catalog.ModuleCategoryIds, id => Assert.Equal(7, id));
    }

    [Fact]
    public async Task Public_v3_detail_is_slug_scoped_and_exposes_structural_selection_with_persisted_offers_only()
    {
        var supplier = Guid.NewGuid();
        var shop = Guid.NewGuid();
        var product = Product.CreateCatalogProduct(
            supplier,
            7,
            ProductType.Service,
            SalesModel.SessionBased,
            FulfillmentMethod.Voucher,
            "خدمت",
            "service-one"
        );
        var repository = new FakeProductRepository([product]);
        await new AddProductImageCommandHandler(repository).Handle(
            new AddProductImageCommand(product.Id, "/service.png", true, 0),
            CancellationToken.None
        );
        await new AddProductSpecificationCommandHandler(repository).Handle(
            new AddProductSpecificationCommand(product.Id, "مدت", "یک ساعت", 0),
            CancellationToken.None
        );

        var pathService = new FakePathService();

        var structural = new ProductV3SubresourceHandlers(
            repository,
            new EligibilityMediator(new HashSet<int>()),
            pathService
        );
        var session = await structural.Handle(
            new CreateProductSessionV3Command(
                Guid.NewGuid(),
                true,
                product.Id,
                "2026-08-15",
                "10:00",
                "11:00",
                5,
                "صبح"
            ),
            CancellationToken.None
        );
        var offerId = Guid.NewGuid();
        var catalog = new FakePublicCatalogRepository([
            PublicCandidate(
                product.Id,
                supplier,
                shop,
                "main-shop",
                product.Title,
                7,
                42_000,
                DateTimeOffset.UtcNow.AddDays(1),
                productSlug: product.Slug,
                sessionId: session.Id,
                offerId: offerId
            ),
        ]);

        var handler = new GetPublicProductDetailV3Handler(
            catalog,
            new FakeStoreModuleRepository(StoreModule.Create("فروشگاه", "store", categoryId: 7)),
            repository,
            new PublicEligibilityMediator(new HashSet<Guid> { supplier }),
            pathService,
            null
        );

        var detail = await handler.Handle(
            new GetPublicProductDetailV3Query(
                "store",
                product.Slug,
                ShopSlug: "main-shop",
                OfferId: offerId,
                SessionId: session.Id
            ),
            CancellationToken.None
        );
        var wrongShop = await handler.Handle(
            new GetPublicProductDetailV3Query("store", product.Slug, ShopSlug: "other-shop"),
            CancellationToken.None
        );

        Assert.NotNull(detail);
        Assert.Null(wrongShop);
        Assert.Equal("Offer", detail.PricingAuthority);
        Assert.Equal(offerId, detail.SelectedOfferId);
        Assert.Single(detail.Images);
        Assert.Single(detail.Specifications);
        Assert.Single(detail.Sessions);
        var offer = Assert.Single(detail.Offers);
        Assert.Equal(session.Id, offer.Selection.SessionId);
        Assert.Equal(shop, offer.Selection.ShopId);
        Assert.DoesNotContain(
            "Synthetic",
            detail.GetType().Name,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Empty(
            typeof(ProductSessionV3Dto)
                .GetProperties()
                .Where(x => x.Name.Contains("Price", StringComparison.OrdinalIgnoreCase))
        );
        Assert.Empty(
            typeof(ProductVariantV3Dto)
                .GetProperties()
                .Where(x => x.Name.Contains("Price", StringComparison.OrdinalIgnoreCase))
        );
    }

    private static PublicCatalogOfferCandidate PublicCandidate(
        Guid productId,
        Guid supplierId,
        Guid shopId,
        string shopSlug,
        string title,
        int categoryId,
        long price,
        DateTimeOffset? endDateUtc,
        string? productSlug = null,
        Guid? variantId = null,
        Guid? sessionId = null,
        Guid? offerId = null
    ) =>
        new(
            offerId ?? Guid.NewGuid(),
            productId,
            shopId,
            supplierId,
            categoryId,
            title,
            productSlug ?? $"p-{productId:N}",
            null,
            ProductType.Goods,
            sessionId.HasValue ? SalesModel.SessionBased : SalesModel.InventoryBased,
            FulfillmentMethod.Shipping,
            DateTimeOffset.UtcNow,
            "/main.png",
            "فروشگاه",
            shopSlug,
            price,
            0,
            price,
            variantId,
            sessionId,
            DateTimeOffset.UtcNow.AddDays(-1),
            endDateUtc
        );

    private static DefaultHttpContext ProviderHttp(
        Guid userId,
        string method,
        string path,
        Guid productId
    )
    {
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")
        );
        http.Request.Method = method;
        http.Request.Path = path;
        http.Request.RouteValues["id"] = productId;
        return http;
    }

    [Fact]
    public async Task Public_products_filter_before_pagination_and_return_exact_total()
    {
        var supplierId = Guid.NewGuid();
        var products = Enumerable
            .Range(1, 5)
            .Select(categoryId =>
                Product.CreateCatalogProduct(
                    supplierId,
                    categoryId,
                    ProductType.Goods,
                    SalesModel.Unlimited,
                    FulfillmentMethod.Shipping,
                    $"محصول {categoryId}",
                    $"product-{categoryId}"
                )
            )
            .ToList();
        var repository = new FakeProductRepository(products);
        var mediator = new EligibilityMediator(new HashSet<int> { 2, 4, 5 });

        using var cts = new CancellationTokenSource();
        var result = await new ListProductsV3Handler(repository, mediator).Handle(
            new ListProductsV3Query(null, null, false, 2, 2),
            cts.Token
        );

        Assert.Equal(3, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(5, result.Items[0].CategoryId);
        Assert.Equal(1, mediator.BatchCalls);
        Assert.Equal(0, mediator.SingleCalls);
        Assert.Equal(3, repository.LastEligibleIds.Count);
        Assert.Equal(cts.Token, repository.LastCancellationToken);
        Assert.Equal(cts.Token, mediator.LastCancellationToken);
    }

    [Fact]
    public async Task Public_offers_filter_before_pagination_and_use_batch_resolver()
    {
        var now = DateTimeOffset.UtcNow;
        var supplierId = Guid.NewGuid();
        var offers = Enumerable
            .Range(1, 5)
            .Select(categoryId =>
                (
                    CategoryId: categoryId,
                    Offer: Offer.Create(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        null,
                        null,
                        10_000,
                        10,
                        now.AddDays(-1),
                        null
                    )
                )
            )
            .ToList();
        var repository = new FakeOfferRepository(offers, supplierId);
        var mediator = new EligibilityMediator(new HashSet<int> { 2, 4, 5 });

        var handler = new OfferQueryHandlers(
            repository,
            new FakeProductRepository([]),
            new StubShopRepository(),
            mediator
        );
        using var cts = new CancellationTokenSource();
        var result = await handler.Handle(
            new ListOffersQuery(null, null, false, now, 2, 2),
            cts.Token
        );

        Assert.Equal(3, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(offers[4].Offer.Id, result.Items[0].Id);
        Assert.Equal(1, mediator.BatchCalls);
        Assert.Equal(0, mediator.SingleCalls);
        Assert.Equal(3, repository.LastEligibleIds.Count);
        Assert.Equal(cts.Token, repository.LastCancellationToken);
        Assert.Equal(cts.Token, mediator.LastCancellationToken);
    }

    private sealed class EligibilityMediator(
        IReadOnlySet<int> eligibleCategories,
        short eligibleChannels = (short)SalesChannel.Online
    ) : IMediator
    {
        public int BatchCalls { get; private set; }
        public int SingleCalls { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default
        )
        {
            LastCancellationToken = cancellationToken;
            object response = request switch
            {
                ResolveAgreementCategoryTermsBatchQuery batch => ResolveBatch(batch),
                ResolveAgreementCategoryTermQuery => CountSingleAndFail(),
                _ => throw new NotSupportedException(request.GetType().FullName),
            };
            return Task.FromResult((TResponse)response);
        }

        private IReadOnlyList<AgreementCategoryTermBatchResult> ResolveBatch(
            ResolveAgreementCategoryTermsBatchQuery query
        )
        {
            BatchCalls++;
            return query
                .Requests.Select(request => new AgreementCategoryTermBatchResult(
                    request,
                    eligibleCategories.Contains(request.CategoryId)
                    && (eligibleChannels & request.SalesChannel) != 0
                        ? new ResolvedAgreementCategoryTermDto(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            request.SupplierId,
                            request.CategoryId,
                            request.CategoryId,
                            request.SalesChannel,
                            0,
                            request.AtUtc.AddDays(-1),
                            request.AtUtc.AddDays(1)
                        )
                        : null
                ))
                .ToArray();
        }

        private object CountSingleAndFail()
        {
            SingleCalls++;
            throw new InvalidOperationException(
                "Single resolver must not be used by list handlers."
            );
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class PublicEligibilityMediator(IReadOnlySet<Guid> eligibleSuppliers) : IMediator
    {
        public int BatchCalls { get; private set; }
        public int SingleCalls { get; private set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken ct = default
        )
        {
            object response = request switch
            {
                ResolveAgreementCategoryTermsBatchQuery batch => Resolve(batch),
                ResolveAgreementCategoryTermQuery => Single(),
                _ => throw new NotSupportedException(request.GetType().FullName),
            };
            return Task.FromResult((TResponse)response);
        }

        private IReadOnlyList<AgreementCategoryTermBatchResult> Resolve(
            ResolveAgreementCategoryTermsBatchQuery query
        )
        {
            BatchCalls++;
            return query
                .Requests.Select(request => new AgreementCategoryTermBatchResult(
                    request,
                    eligibleSuppliers.Contains(request.SupplierId)
                        ? new ResolvedAgreementCategoryTermDto(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            request.SupplierId,
                            request.CategoryId,
                            request.CategoryId,
                            request.SalesChannel,
                            0,
                            request.AtUtc.AddMinutes(-1),
                            request.AtUtc.AddMinutes(1)
                        )
                        : null
                ))
                .ToArray();
        }

        private object Single()
        {
            SingleCalls++;
            throw new InvalidOperationException("N+1 resolver is forbidden.");
        }

        public Task<object?> Send(object request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken ct = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class FakePublicCatalogRepository(List<PublicCatalogOfferCandidate> candidates)
        : IPublicCatalogRepository
    {
        public List<int?> ModuleCategoryIds { get; } = [];

        public Task<IReadOnlyList<PublicCatalogOfferCandidate>> GetEffectiveCandidatesAsync(
            int? moduleCategoryId,
            int? categoryId,
            Guid? shopId,
            string? shopSlug,
            string? productSlug,
            string? search,
            SalesModel? salesModel,
            DateTimeOffset atUtc,
            CancellationToken ct = default
        )
        {
            ModuleCategoryIds.Add(moduleCategoryId);
            var query = candidates.Where(x =>
                x.StartDateUtc <= atUtc && (!x.EndDateUtc.HasValue || atUtc < x.EndDateUtc.Value)
            );
            if (moduleCategoryId.HasValue)
                query = query.Where(x => x.CategoryId == moduleCategoryId);
            if (categoryId.HasValue)
                query = query.Where(x => x.CategoryId == categoryId);
            if (shopId.HasValue)
                query = query.Where(x => x.ShopId == shopId);
            if (!string.IsNullOrWhiteSpace(shopSlug))
                query = query.Where(x =>
                    x.ShopSlug.Equals(shopSlug, StringComparison.OrdinalIgnoreCase)
                );
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x =>
                    x.ProductTitle.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.ShopName.Contains(search, StringComparison.OrdinalIgnoreCase)
                );
            if (salesModel.HasValue)
                query = query.Where(x => x.SalesModel == salesModel);
            return Task.FromResult<IReadOnlyList<PublicCatalogOfferCandidate>>(query.ToArray());
        }
    }

    private sealed class FakeStoreModuleRepository(StoreModule module) : IStoreModuleRepository
    {
        public Task<StoreModule?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult<StoreModule?>(
                module.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase) ? module : null
            );

        public Task<StoreModule?> GetByIdAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<StoreModule?>(null);

        public Task<List<StoreModule>> GetAllAsync(
            bool includeInactive = false,
            CancellationToken ct = default
        ) => Task.FromResult(new List<StoreModule> { module });

        public Task<bool> SlugExistsAsync(
            string slug,
            int? excludeId = null,
            CancellationToken ct = default
        ) => Task.FromResult(module.Slug == slug);

        public Task AddAsync(StoreModule value, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(StoreModule value, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class LegacyAgreementMediator(Guid agreementProductId) : IMediator
    {
        public int AgreementCalls { get; private set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken ct = default
        )
        {
            if (
                request is not GetAgreementProductByIdQuery query
                || query.ProductId != agreementProductId
            )
                throw new NotSupportedException(request.GetType().FullName);
            AgreementCalls++;
#pragma warning disable CS0618
            object response = new AgreementProductDto(
                agreementProductId,
                Guid.NewGuid(),
                "legacy",
                null,
                1,
                null,
                1,
                1,
                (short)SalesModel.InventoryBased,
                0,
                false,
                DateTimeOffset.UtcNow
            );
#pragma warning restore CS0618
            return Task.FromResult((TResponse)response);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken ct = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class FakePathService : IPathService
    {
        public string MakeAbsoluteMediaUrl(string mediaPath) => mediaPath;
    }

    private sealed class FilterMediator(
        Guid allowedSupplierId,
        Guid? legacyAgreementProductId = null
    ) : IMediator
    {
        private readonly Guid _agreementId = Guid.NewGuid();
        public int AgreementLookups { get; private set; }
        public string? LastPermission { get; private set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken ct = default
        )
        {
            object? response = request switch
            {
                AuthorizeStoreResourceQuery authorization => Authorize(authorization),
                GetAgreementProductByIdQuery query
                    when legacyAgreementProductId == query.ProductId => LegacyProduct(
                    query.ProductId
                ),
                GetAgreementByIdQuery query when query.Id == _agreementId => LegacyAgreement(),
                GetAgreementProductByIdQuery => throw new InvalidOperationException(
                    "V3 product must not resolve AgreementProduct."
                ),
                _ => throw new NotSupportedException(request.GetType().FullName),
            };
            return Task.FromResult((TResponse)response!);
        }

        private bool Authorize(AuthorizeStoreResourceQuery query)
        {
            LastPermission = query.Permission;
            return query.VendorId == allowedSupplierId
                && query.ShopId is null
                && query.Permission == StorePermissions.ManageCatalog;
        }
#pragma warning disable CS0618
        private AgreementProductDto LegacyProduct(Guid id)
        {
            AgreementLookups++;
            return new(
                id,
                _agreementId,
                "legacy",
                null,
                1,
                null,
                1,
                1,
                1,
                0,
                false,
                DateTimeOffset.UtcNow
            );
        }

        private AgreementDto LegacyAgreement() =>
            new(
                _agreementId,
                "A",
                1,
                "type",
                allowedSupplierId,
                "supplier",
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(1),
                3,
                "active",
                null,
                false,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                [],
                []
            );
#pragma warning restore CS0618
        public Task<object?> Send(object request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken ct = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class DetailCompositionMediator(ProductV3Dto product, ProductDetailDto detail)
        : IMediator
    {
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken ct = default
        )
        {
            object? response = request switch
            {
                GetProductV3Query query when query.ProductId == product.Id => product,
                AdminGetProductQuery query when query.ProductId == product.Id => detail,
                _ => throw new NotSupportedException(request.GetType().FullName),
            };
            return Task.FromResult((TResponse)response!);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken ct = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class VendorContextMediator(Guid userId, StoreVendorContextDto context)
        : IMediator
    {
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken ct = default
        )
        {
            object response =
                request is GetStoreVendorContextsQuery query && query.UserId == userId
                    ? new[] { context }
                    : throw new NotSupportedException(request.GetType().FullName);
            return Task.FromResult((TResponse)response);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken ct = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class FakeProductRepository(List<Product> candidates) : IProductRepository
    {
        public Product? AddedProduct { get; private set; }
        public IReadOnlyCollection<Guid> LastEligibleIds { get; private set; } = [];
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<List<Product>> GetCatalogEligibilityCandidatesAsync(
            Guid? supplierId,
            int? categoryId,
            CancellationToken ct = default
        )
        {
            LastCancellationToken = ct;
            return Task.FromResult(candidates);
        }

        public Task<(List<Product> Items, int Total)> GetCatalogPageByIdsAsync(
            IReadOnlyCollection<Guid> eligibleIds,
            int page,
            int pageSize,
            CancellationToken ct = default
        )
        {
            LastCancellationToken = ct;
            LastEligibleIds = eligibleIds;
            var eligible = candidates.Where(x => eligibleIds.Contains(x.Id)).ToList();
            return Task.FromResult(
                (eligible.Skip((page - 1) * pageSize).Take(pageSize).ToList(), eligible.Count)
            );
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(candidates.FirstOrDefault(x => x.Id == id));

        public Task<Product?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(candidates.FirstOrDefault(x => x.Id == id));

        public Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Product?> GetDisplayableBySlugAsync(
            string slug,
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(List<Product> Items, int Total)> GetPagedAsync(
            Guid? shopId,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(List<Product> Items, int Total)> GetPagedAdminAsync(
            Guid? shopId,
            bool? isDeleted,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(List<Product> Items, int Total)> GetCatalogPagedAsync(
            Guid? supplierId,
            int? categoryId,
            bool includeInactive,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(List<Product> Items, int Total)> SearchAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(List<Product> Items, int Total)> SearchAsync(
            string query,
            IReadOnlyList<Guid> ids,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<List<Product>> GetByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<List<Product>> GetByIdsForAdminWithDetailsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task AddAsync(Product product, CancellationToken ct = default)
        {
            AddedProduct = product;
            candidates.Add(product);
            return Task.CompletedTask;
        }

        public Task AddVariantAttributeAsync(
            Product product,
            VariantAttribute attribute,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task AddVariantAttributeValueAsync(
            Product product,
            VariantAttributeValue value,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task AddProductVariantAsync(
            Product product,
            ProductVariant variant,
            CancellationToken ct = default
        ) => Task.CompletedTask;

        public Task UpdateAsync(Product product, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeOfferRepository(
        List<(int CategoryId, Offer Offer)> candidates,
        Guid supplierId
    ) : IOfferRepository
    {
        public IReadOnlyCollection<Guid> LastEligibleIds { get; private set; } = [];
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<IReadOnlyList<OfferEligibilityCandidate>> GetEligibilityCandidatesAsync(
            Guid? productId,
            Guid? shopId,
            DateTimeOffset atUtc,
            CancellationToken ct = default
        )
        {
            LastCancellationToken = ct;
            return Task.FromResult<IReadOnlyList<OfferEligibilityCandidate>>(
                candidates
                    .Select(x => new OfferEligibilityCandidate(
                        x.Offer.Id,
                        supplierId,
                        x.CategoryId,
                        (short)SalesChannel.Online
                    ))
                    .ToArray()
            );
        }

        public Task<(IReadOnlyList<Offer> Items, int Total)> GetPageByIdsAsync(
            IReadOnlyCollection<Guid> eligibleIds,
            int page,
            int size,
            CancellationToken ct = default
        )
        {
            LastCancellationToken = ct;
            LastEligibleIds = eligibleIds;
            var eligible = candidates
                .Select(x => x.Offer)
                .Where(x => eligibleIds.Contains(x.Id))
                .ToList();
            return Task.FromResult(
                (
                    (IReadOnlyList<Offer>)eligible.Skip((page - 1) * size).Take(size).ToList(),
                    eligible.Count
                )
            );
        }

        public Task<Offer?> GetByIdAsync(
            Guid id,
            bool includeDeleted = false,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<bool> HasOpenEndedCoordinateAsync(
            Guid productId,
            Guid shopId,
            Guid? variantId,
            Guid? sessionId,
            Guid? excludingId = null,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(IReadOnlyList<Offer> Items, int Total)> GetPagedAsync(
            Guid? productId,
            Guid? shopId,
            bool includeDeleted,
            DateTimeOffset? at,
            int page,
            int size,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<Offer?> ResolveAsync(
            Guid productId,
            Guid shopId,
            Guid? variantId,
            Guid? sessionId,
            DateTimeOffset atUtc,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task AddAsync(Offer offer, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(
            Offer offer,
            uint expectedVersion,
            CancellationToken ct = default
        ) => throw new NotSupportedException();
    }

    private sealed class StubShopRepository : IShopRepository
    {
        public Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Shop?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Shop?> GetByProviderIdAsync(Guid providerId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<List<Shop>> GetBySupplierIdAsync(
            Guid supplierId,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(List<Shop> Items, int Total)> GetPagedAsync(
            ShopType? shopType,
            ShopStatus? status,
            int page,
            int size,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> ProviderHasShopAsync(Guid providerId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<(List<Shop> Items, int Total)> GetPagedByIdsAsync(
            IEnumerable<Guid> ids,
            int page,
            int size,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<List<Shop>> GetByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task AddAsync(Shop shop, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(Shop shop, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubProductSessionRepository : IProductSessionRepository
    {
        public Task<ProductSession?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ProductSession?>(null);

        public Task<List<ProductSession>> GetByProductIdAsync(
            Guid productId,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<List<ProductSession>> GetByProductIdAndDateAsync(
            Guid productId,
            DateOnly date,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<List<ProductSession>> GetAvailableByProductIdAsync(
            Guid productId,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task UpdateAsync(ProductSession session, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubShopProductRepository : IShopProductRepository
    {
        public Task<(
            IReadOnlyList<ProductOfferingReadModel> Items,
            int Total
        )> GetDisplayableProductsAsync(
            IReadOnlyList<Guid> stockIds,
            IReadOnlyList<Guid> sessionIds,
            DateOnly today,
            string? search,
            string sort,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<ShopProduct?> GetAsync(
            Guid shopId,
            Guid productId,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<ShopProduct?> GetWithVariantOfferingsAsync(
            Guid shopId,
            Guid productId,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<ShopProduct?> GetBestDisplayableForProductAsync(
            Guid productId,
            SalesModel salesModel,
            DateOnly today,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(List<ShopProduct> Items, int Total)> GetByShopAsync(
            Guid shopId,
            bool? active,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(List<ShopProduct> Items, int Total)> GetByProductAsync(
            Guid productId,
            bool? active,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) =>
            throw new InvalidOperationException(
                "V3 detail must not read legacy ShopProduct pricing."
            );

        public Task<IReadOnlyList<ShopProduct>> ListForVariantBackfillAsync(
            Guid? shopId = null,
            Guid? productId = null,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetActiveShopIdsByAgreementProductIdsAsync(
            IEnumerable<Guid> ids,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetDisplayableShopIdsByAgreementProductIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(
            IReadOnlyList<Guid> ProductIds,
            int Total
        )> GetDisplayableProductIdsByAgreementProductIdsAsync(
            IReadOnlyList<Guid> ids,
            Guid? shopId,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, ShopProduct>> GetForProductsAsync(
            IReadOnlyList<Guid> ids,
            Guid? shopId = null,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task AddAsync(ShopProduct value, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddVariantOfferingsAsync(
            ShopProduct value,
            IReadOnlyList<ShopProductVariant> offerings,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task UpsertVariantOfferingAsync(
            ShopProduct value,
            ShopProductVariant offering,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task UpdateAsync(ShopProduct value, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubReviewRepository : IReviewRepository
    {
        public Task<Review?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<List<Review>> GetByProductIdAsync(
            Guid productId,
            bool approvedOnly = true,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<bool> UserHasReviewedAsync(
            Guid productId,
            Guid userId,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(List<Review> Items, int Total)> GetPagedAsync(
            Guid productId,
            bool approvedOnly,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => Task.FromResult((new List<Review>(), 0));

        public Task<double> GetAverageRatingAsync(Guid productId, CancellationToken ct = default) =>
            Task.FromResult(0d);

        public Task AddAsync(Review review, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(Review review, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class IdentityPathService : IPathService
    {
        public string MakeAbsoluteMediaUrl(string mediaPath) => mediaPath;
    }
}
