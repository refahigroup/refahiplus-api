using MediatR;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Application.Features.Products.GetProductBySlug;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using Refahi.Shared.Services.Path;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class GetProductBySlugEligibilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ReturnsOnlyInStockShopVariants_AndUsesCheapestEffectivePrice()
    {
        var product = Product.Create(Guid.NewGuid(), "محصول", "product", stockCount: 10);
        var expensive = product.AddVariant([], 5, 5_000, sku: "expensive");
        var cheapest = product.AddVariant([], 3, 4_000, sku: "cheapest");
        var outOfStock = product.AddVariant([], 0, 1_000, sku: "out");
        var shop = CreateActiveShop();
        var shopProduct = ShopProduct.Create(shop.Id, product.Id, 9_000, 8_000);
        shopProduct.AddVariantOffering(expensive.Id, 5_000, 4_500, isActive: true);
        shopProduct.AddVariantOffering(cheapest.Id, 4_000, 3_000, isActive: true);
        shopProduct.AddVariantOffering(outOfStock.Id, 1_000, null, isActive: true);

        var handler = CreateHandler(product, shop, shopProduct, SalesModel.StockBased);
        var result = await handler.Handle(
            new GetProductBySlugQuery(7, product.Slug, shop.Slug),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(4_000, result.PriceMinor);
        Assert.Equal(3_000, result.DiscountedPriceMinor);
        Assert.Equal([expensive.Id, cheapest.Id], result.Variants.Select(x => x.Id));
        Assert.All(result.Variants, variant => Assert.True(variant.IsAvailable));
        Assert.DoesNotContain(result.Variants, variant => variant.Id == outOfStock.Id);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenProductIsOutsideModuleCatalog()
    {
        var product = Product.Create(Guid.NewGuid(), "محصول", "outside-module", stockCount: 1);
        var variant = product.AddVariant([], 1, 1_000);
        var shop = CreateActiveShop();
        var shopProduct = ShopProduct.Create(shop.Id, product.Id, 1_000, 0);
        shopProduct.AddVariantOffering(variant.Id, 1_000, null, isActive: true);

        var handler = CreateHandler(product, shop, shopProduct, SalesModel.StockBased, catalogContainsProduct: false);
        var result = await handler.Handle(
            new GetProductBySlugQuery(7, product.Slug, shop.Slug),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyFutureAvailableSessions_ForSessionBasedProduct()
    {
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        var product = Product.Create(Guid.NewGuid(), "محصول جلسه‌ای", "session-product", stockCount: 1);
        var variant = product.AddVariant(
            [], 0, 2_000, capacityType: VariantCapacityType.Unlimited, salesModel: SalesModel.SessionBased);
        product.AddSession(today.AddDays(-1), new TimeOnly(10, 0), new TimeOnly(11, 0), 10);
        product.AddSession(today.AddDays(1), new TimeOnly(10, 0), new TimeOnly(11, 0), 10, "آینده");
        product.AddSession(today.AddDays(2), new TimeOnly(10, 0), new TimeOnly(11, 0), 10, "لغوشده");
        product.Sessions[^1].Cancel();

        var shop = CreateActiveShop();
        var shopProduct = ShopProduct.Create(shop.Id, product.Id, 2_000, 0);
        shopProduct.AddVariantOffering(variant.Id, 2_000, null, isActive: true);
        var handler = CreateHandler(product, shop, shopProduct, SalesModel.SessionBased);

        var result = await handler.Handle(
            new GetProductBySlugQuery(7, product.Slug, shop.Slug),
            CancellationToken.None);

        Assert.NotNull(result);
        var session = Assert.Single(result.Sessions!);
        Assert.Equal(today.AddDays(1).ToString("yyyy-MM-dd"), session.Date);
        Assert.Equal("آینده", session.Title);
    }

    private static GetProductBySlugQueryHandler CreateHandler(
        Product product,
        Shop shop,
        ShopProduct shopProduct,
        SalesModel salesModel,
        bool catalogContainsProduct = true)
        => new(
            new FakeProductRepository(product),
            new FakeShopRepository(shop),
            new FakeShopProductRepository(shopProduct),
            new FakeReviewRepository(),
            new FakeMediator(product.AgreementProductId, salesModel),
            new FakePathService(),
            new FakeCatalog(catalogContainsProduct ? [product.AgreementProductId] : []),
            new FixedTimeProvider(Now));

    private static Shop CreateActiveShop()
    {
        var shop = Shop.Create("فروشگاه", "shop", ShopType.Online, Guid.NewGuid());
        shop.Approve();
        return shop;
    }

    private sealed class FakeCatalog(IReadOnlyList<Guid> ids) : IStoreModuleCatalogService
    {
        public Task<IReadOnlyList<Guid>> GetDisplayableAgreementProductIdsAsync(int moduleId, CancellationToken ct = default)
            => Task.FromResult(ids);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakePathService : IPathService
    {
        public string MakeAbsoluteMediaUrl(string mediaPath) => mediaPath;
    }

    private sealed class FakeReviewRepository : IReviewRepository
    {
        public Task<(List<Review> Items, int Total)> GetPagedAsync(Guid productId, bool approvedOnly, int page, int pageSize, CancellationToken ct = default) => Task.FromResult((new List<Review>(), 0));
        public Task<double> GetAverageRatingAsync(Guid productId, CancellationToken ct = default) => Task.FromResult(0d);
        public Task<Review?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Review>> GetByProductIdAsync(Guid productId, bool approvedOnly = true, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UserHasReviewedAsync(Guid productId, Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Review review, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Review review, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeProductRepository(Product product) : IProductRepository
    {
        public Task<Product?> GetDisplayableBySlugAsync(string slug, IReadOnlyList<Guid> allowedAgreementProductIds, CancellationToken ct = default)
            => Task.FromResult(product.Slug == slug && allowedAgreementProductIds.Contains(product.AgreementProductId) ? product : null);
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Product?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Product> Items, int Total)> GetPagedAsync(Guid? shopId, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Product> Items, int Total)> GetPagedAdminAsync(Guid? shopId, bool? isDeleted, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Product> Items, int Total)> SearchAsync(string query, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Product> Items, int Total)> SearchAsync(string query, IReadOnlyList<Guid> allowedAgreementProductIds, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Product>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Product>> GetByIdsForAdminWithDetailsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Product value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddVariantAttributeAsync(Product value, VariantAttribute attribute, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddVariantAttributeValueAsync(Product value, VariantAttributeValue attributeValue, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddProductVariantAsync(Product value, ProductVariant variant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Product value, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeShopRepository(Shop shop) : IShopRepository
    {
        public Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(id == shop.Id ? shop : null);
        public Task<Shop?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(slug == shop.Slug ? shop : null);
        public Task<Shop?> GetByProviderIdAsync(Guid providerId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Shop> Items, int Total)> GetPagedAsync(ShopType? shopType, ShopStatus? status, int page, int size, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ProviderHasShopAsync(Guid providerId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Shop> Items, int Total)> GetPagedByIdsAsync(IEnumerable<Guid> ids, int page, int size, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Shop>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Shop value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Shop value, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeShopProductRepository(ShopProduct shopProduct) : IShopProductRepository
    {
        public Task<ShopProduct?> GetWithVariantOfferingsAsync(Guid shopId, Guid productId, CancellationToken ct = default)
            => Task.FromResult(shopProduct.ShopId == shopId && shopProduct.ProductId == productId ? shopProduct : null);
        public Task<ShopProduct?> GetBestDisplayableForProductAsync(Guid productId, SalesModel salesModel, DateOnly today, CancellationToken ct = default)
            => Task.FromResult(shopProduct.ProductId == productId ? shopProduct : null);
        public Task<(IReadOnlyList<ProductOfferingReadModel> Items, int Total)> GetDisplayableProductsAsync(IReadOnlyList<Guid> stockBasedAgreementProductIds, IReadOnlyList<Guid> sessionBasedAgreementProductIds, DateOnly today, string? searchQuery, string sort, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ShopProduct?> GetAsync(Guid shopId, Guid productId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<ShopProduct> Items, int Total)> GetByShopAsync(Guid shopId, bool? isActive, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<ShopProduct> Items, int Total)> GetByProductAsync(Guid productId, bool? isActive, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ShopProduct>> ListForVariantBackfillAsync(Guid? shopId = null, Guid? productId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetActiveShopIdsByAgreementProductIdsAsync(IEnumerable<Guid> agreementProductIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetDisplayableShopIdsByAgreementProductIdsAsync(IReadOnlyList<Guid> apIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<Guid> ProductIds, int Total)> GetDisplayableProductIdsByAgreementProductIdsAsync(IReadOnlyList<Guid> apIds, Guid? shopId, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, ShopProduct>> GetForProductsAsync(IReadOnlyList<Guid> productIds, Guid? shopId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(ShopProduct value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddVariantOfferingsAsync(ShopProduct value, IReadOnlyList<ShopProductVariant> offerings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertVariantOfferingAsync(ShopProduct value, ShopProductVariant offering, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(ShopProduct value, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeMediator(Guid agreementProductId, SalesModel salesModel) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object response = request switch
            {
                GetAgreementProductByIdQuery query when query.ProductId == agreementProductId => new AgreementProductDto(
                    agreementProductId, Guid.NewGuid(), "محصول قرارداد", null, 1, null,
                    (short)ProductType.Physical, (short)DeliveryType.Shipping, (short)salesModel,
                    0, false, Now),
                _ => throw new NotSupportedException(request.GetType().FullName)
            };
            return Task.FromResult((TResponse)response);
        }
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
