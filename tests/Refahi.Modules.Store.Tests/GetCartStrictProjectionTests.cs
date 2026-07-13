using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Cart;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;
using Refahi.Modules.Store.Application.Features.Cart.GetCart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class GetCartStrictProjectionTests
{
    private static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ShopId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Handle_MarksVariantUnavailable_WhenShopProductVariantIsInactive()
    {
        var fixture = TestFixture.Create(cartUnitPrice: 1700);
        fixture.PriceResolver.ExceptionToThrow = new StoreDomainException(
            "این تنوع در فروشگاه انتخاب‌شده فعال نیست.",
            "SHOP_PRODUCT_VARIANT_INACTIVE");

        var result = await fixture.Handler.Handle(new GetCartQuery(UserId, fixture.Cart.ModuleId), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.False(item.IsAvailable);
        Assert.Null(item.CurrentUnitPriceMinor);
        Assert.False(item.HasPriceChanged);
        Assert.Null(item.ShopProductVariantId);
        Assert.Null(item.PriceSource);
    }

    [Fact]
    public async Task Handle_MarksPriceChanged_WhenShopProductVariantPriceChanges()
    {
        var fixture = TestFixture.Create(cartUnitPrice: 1700, currentUnitPrice: 1900);

        var result = await fixture.Handler.Handle(new GetCartQuery(UserId, fixture.Cart.ModuleId), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(item.IsAvailable);
        Assert.Equal(1900, item.CurrentUnitPriceMinor);
        Assert.True(item.HasPriceChanged);
        Assert.Equal(fixture.ShopProductVariantId, item.ShopProductVariantId);
        Assert.Equal(StorePriceSource.ShopProductVariant.ToString(), item.PriceSource);
        Assert.Equal(2200, item.OriginalUnitPriceMinor);
    }

    private sealed class TestFixture
    {
        private TestFixture(
            Cart cart,
            Guid shopProductVariantId,
            FakePriceResolver priceResolver,
            GetCartQueryHandler handler)
        {
            Cart = cart;
            ShopProductVariantId = shopProductVariantId;
            PriceResolver = priceResolver;
            Handler = handler;
        }

        public Cart Cart { get; }
        public Guid ShopProductVariantId { get; }
        public FakePriceResolver PriceResolver { get; }
        public GetCartQueryHandler Handler { get; }

        public static TestFixture Create(long cartUnitPrice, long currentUnitPrice = 1700)
        {
            var agreementProductId = Guid.NewGuid();
            var product = Product.Create(agreementProductId, "محصول تست", "test-product", stockCount: 10);
            var variant = product.AddVariant([], stockCount: 10, priceMinor: 2200, discountedPriceMinor: 1700, sku: "1cc");

            var cart = Cart.Create(UserId, moduleId: 1);
            cart.AddItem(ShopId, product.Id, variant.Id, sessionId: null, usageDate: null, quantity: 1, cartUnitPrice);

            var shop = Shop.Create("فروشگاه تست", "test-shop", ShopType.Online, Guid.NewGuid());
            shop.Approve();

            var shopProductId = Guid.NewGuid();
            var shopProductVariantId = Guid.NewGuid();
            var priceResolver = new FakePriceResolver(new StoreResolvedPrice(
                UnitPriceMinor: currentUnitPrice,
                OriginalPriceMinor: 2200,
                DiscountedPriceMinor: currentUnitPrice == 2200 ? null : currentUnitPrice,
                ShopProductId: shopProductId,
                ShopProductVariantId: shopProductVariantId,
                VariantId: variant.Id,
                Source: StorePriceSource.ShopProductVariant));

            var handler = new GetCartQueryHandler(
                new FakeCartRepository(cart),
                new FakeProductRepository(product),
                new FakeProductSessionRepository(),
                new FakeShopRepository(shop),
                new FakeShopProductRepository(),
                priceResolver,
                new FakeMediator(product.AgreementProductId));

            return new TestFixture(cart, shopProductVariantId, priceResolver, handler);
        }
    }

    private sealed class FakeCartRepository : ICartRepository
    {
        private readonly Cart _cart;

        public FakeCartRepository(Cart cart) => _cart = cart;

        public Task<Cart?> GetByUserAndModuleIdAsync(Guid userId, int moduleId, CancellationToken ct = default) =>
            Task.FromResult(_cart.UserId == userId && _cart.ModuleId == moduleId ? _cart : null);

        public Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_cart.UserId == userId ? _cart : null);

        public Task<Cart?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_cart.Id == id ? _cart : null);

        public Task<Cart> AddItemAsync(Guid userId, int moduleId, Guid shopId, Guid productId, Guid? variantId, Guid? sessionId, DateOnly? usageDate, int quantity, long unitPriceMinor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddAsync(Cart cart, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Cart cart, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Cart cart, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly Product _product;

        public FakeProductRepository(Product product) => _product = product;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_product.Id == id ? _product : null);

        public Task<Product?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Product?> GetDisplayableBySlugAsync(string slug, IReadOnlyList<Guid> allowedAgreementProductIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Product> Items, int Total)> GetPagedAsync(Guid? shopId, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Product> Items, int Total)> GetPagedAdminAsync(Guid? shopId, bool? isDeleted, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Product> Items, int Total)> SearchAsync(string query, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Product> Items, int Total)> SearchAsync(string query, IReadOnlyList<Guid> allowedAgreementProductIds, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Product>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Product>> GetByIdsForAdminWithDetailsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Product product, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddVariantAttributeAsync(Product product, VariantAttribute attribute, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddVariantAttributeValueAsync(Product product, VariantAttributeValue value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddProductVariantAsync(Product product, ProductVariant variant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Product product, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeProductSessionRepository : IProductSessionRepository
    {
        public Task<ProductSession?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ProductSession?>(null);
        public Task<List<ProductSession>> GetByProductIdAsync(Guid productId, CancellationToken ct = default) => Task.FromResult(new List<ProductSession>());
        public Task<List<ProductSession>> GetByProductIdAndDateAsync(Guid productId, DateOnly date, CancellationToken ct = default) => Task.FromResult(new List<ProductSession>());
        public Task<List<ProductSession>> GetAvailableByProductIdAsync(Guid productId, CancellationToken ct = default) => Task.FromResult(new List<ProductSession>());
        public Task UpdateAsync(ProductSession session, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeShopRepository : IShopRepository
    {
        private readonly Shop _shop;

        public FakeShopRepository(Shop shop) => _shop = shop;

        public Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(id == ShopId ? _shop : null);

        public Task<Shop?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Shop?> GetByProviderIdAsync(Guid providerId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Shop> Items, int Total)> GetPagedAsync(ShopType? shopType, ShopStatus? status, int page, int size, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ProviderHasShopAsync(Guid providerId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<Shop> Items, int Total)> GetPagedByIdsAsync(IEnumerable<Guid> ids, int page, int size, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Shop>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Shop shop, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Shop shop, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeShopProductRepository : IShopProductRepository
    {
        public Task<(IReadOnlyList<ProductOfferingReadModel> Items, int Total)> GetDisplayableProductsAsync(IReadOnlyList<Guid> stockBasedAgreementProductIds, IReadOnlyList<Guid> sessionBasedAgreementProductIds, DateOnly today, string? searchQuery, string sort, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ShopProduct?> GetAsync(Guid shopId, Guid productId, CancellationToken ct = default) => Task.FromResult<ShopProduct?>(null);
        public Task<ShopProduct?> GetWithVariantOfferingsAsync(Guid shopId, Guid productId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ShopProduct?> GetBestDisplayableForProductAsync(Guid productId, SalesModel salesModel, DateOnly today, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<ShopProduct> Items, int Total)> GetByShopAsync(Guid shopId, bool? isActive, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(List<ShopProduct> Items, int Total)> GetByProductAsync(Guid productId, bool? isActive, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ShopProduct>> ListForVariantBackfillAsync(Guid? shopId = null, Guid? productId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetActiveShopIdsByAgreementProductIdsAsync(IEnumerable<Guid> agreementProductIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetDisplayableShopIdsByAgreementProductIdsAsync(IReadOnlyList<Guid> apIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<Guid> ProductIds, int Total)> GetDisplayableProductIdsByAgreementProductIdsAsync(IReadOnlyList<Guid> apIds, Guid? shopId, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, ShopProduct>> GetForProductsAsync(IReadOnlyList<Guid> productIds, Guid? shopId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(ShopProduct shopProduct, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddVariantOfferingsAsync(ShopProduct shopProduct, IReadOnlyList<ShopProductVariant> offerings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertVariantOfferingAsync(ShopProduct shopProduct, ShopProductVariant offering, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(ShopProduct shopProduct, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakePriceResolver : IStoreProductPriceResolver
    {
        private readonly StoreResolvedPrice _resolvedPrice;

        public FakePriceResolver(StoreResolvedPrice resolvedPrice) => _resolvedPrice = resolvedPrice;

        public StoreDomainException? ExceptionToThrow { get; set; }

        public Task<StoreResolvedPrice> ResolveAsync(Guid shopId, Guid productId, Guid? variantId, CancellationToken cancellationToken = default) =>
            ExceptionToThrow is null ? Task.FromResult(_resolvedPrice) : Task.FromException<StoreResolvedPrice>(ExceptionToThrow);

        public Task<StoreResolvedPrice> ResolveAsync(Guid shopId, Product product, Guid? variantId, CancellationToken cancellationToken = default) =>
            ExceptionToThrow is null ? Task.FromResult(_resolvedPrice) : Task.FromException<StoreResolvedPrice>(ExceptionToThrow);
    }

    private sealed class FakeMediator : IMediator
    {
        private readonly Guid _agreementProductId;

        public FakeMediator(Guid agreementProductId) => _agreementProductId = agreementProductId;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object? response = request switch
            {
                GetAgreementProductByIdQuery query when query.ProductId == _agreementProductId => new AgreementProductDto(
                    _agreementProductId,
                    AgreementId: Guid.NewGuid(),
                    Name: "agreement product",
                    Description: null,
                    CategoryId: 1,
                    CategoryName: "store",
                    ProductType: 1,
                    DeliveryType: 1,
                    SalesModel: (short)SalesModel.StockBased,
                    CommissionPercent: 0,
                    IsDeleted: false,
                    CreatedAt: DateTimeOffset.UtcNow),
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
