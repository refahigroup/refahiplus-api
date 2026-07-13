using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;
using Refahi.Modules.Store.Application.Features.Checkout.PlaceStoreOrder;
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

public sealed class PlaceStoreOrderStrictValidationTests
{
    private static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ShopId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    [Fact]
    public async Task Handle_CreatesUnpaidOrderWithoutWalletAllocations_WhenVariantIsStrictlyResolved()
    {
        var fixture = TestFixture.StockVariant(cartUnitPrice: 1700, currentUnitPrice: 1700);

        var result = await fixture.Handler.Handle(
            Command(fixture.Cart.ModuleId),
            CancellationToken.None);

        var orderCommand = fixture.Mediator.CreateOrderCommand!;
        var item = Assert.Single(orderCommand.Items);

        Assert.Equal(CapturingMediator.CreatedOrderId, result.OrderId);
        Assert.Equal(1700, result.FinalAmountMinor);
        Assert.Equal("Store", orderCommand.SourceModule);
        Assert.Equal(ShopId, orderCommand.SourceReferenceId);
        Assert.Equal("Unpaid", result.Status);
        Assert.Equal(1700, item.UnitPriceMinor);
        Assert.Contains("\"price_source\":\"ShopProductVariant\"", item.MetadataJson);
        Assert.Contains(fixture.ShopProductVariantId.ToString(), item.MetadataJson);
    }

    [Fact]
    public async Task Handle_ReturnsExistingOrder_WhenSuccessfulCheckoutIsRetriedAfterCartIsCleared()
    {
        var fixture = TestFixture.StockVariant(cartUnitPrice: 1700, currentUnitPrice: 1700);
        const string idempotencyKey = "phase29-retry-success";
        var command = Command(fixture.Cart.ModuleId, idempotencyKey);

        var firstResult = await fixture.Handler.Handle(command, CancellationToken.None);
        fixture.Cart.Clear();

        var retryResult = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.Equal(firstResult.OrderId, retryResult.OrderId);
        Assert.Equal(firstResult.OrderNumber, retryResult.OrderNumber);
        Assert.Equal(firstResult.FinalAmountMinor, retryResult.FinalAmountMinor);
        Assert.Equal(1, fixture.Mediator.CreateOrderCallCount);
        Assert.Equal(1, fixture.CartRepository.GetByUserAndModuleIdCallCount);
    }

    [Fact]
    public async Task Handle_ReturnsExistingOrder_WhenSameIdempotencyKeyIsRetriedWithChangedBody()
    {
        var fixture = TestFixture.StockVariant(cartUnitPrice: 1700, currentUnitPrice: 1700);
        const string idempotencyKey = "phase29-changed-body";

        var firstResult = await fixture.Handler.Handle(
            Command(fixture.Cart.ModuleId, idempotencyKey),
            CancellationToken.None);

        var retryResult = await fixture.Handler.Handle(
            Command(fixture.Cart.ModuleId, idempotencyKey) with { DeliveryTimeSlot = 2 },
            CancellationToken.None);

        Assert.Equal(firstResult.OrderId, retryResult.OrderId);
        Assert.Equal(firstResult.OrderNumber, retryResult.OrderNumber);
        Assert.Equal(1, fixture.Mediator.CreateOrderCallCount);
        Assert.Equal(1, fixture.CartRepository.GetByUserAndModuleIdCallCount);
    }

    [Fact]
    public async Task Handle_AllowsRetry_WhenFirstAttemptFailsBeforeOrderCreation()
    {
        var fixture = TestFixture.StockVariant(cartUnitPrice: 1700, currentUnitPrice: 1700);
        const string idempotencyKey = "phase29-failed-then-fixed";

        fixture.PriceResolver.ExceptionToThrow = new StoreDomainException(
            "این تنوع در فروشگاه انتخاب‌شده فعال نیست.",
            "SHOP_PRODUCT_VARIANT_INACTIVE");

        var ex = await Assert.ThrowsAsync<StoreDomainException>(() => fixture.Handler.Handle(
            Command(fixture.Cart.ModuleId, idempotencyKey),
            CancellationToken.None));

        Assert.Equal("SHOP_PRODUCT_VARIANT_INACTIVE", ex.ErrorCode);
        Assert.Equal(0, fixture.Mediator.CreateOrderCallCount);

        fixture.PriceResolver.ExceptionToThrow = null;

        var result = await fixture.Handler.Handle(
            Command(fixture.Cart.ModuleId, idempotencyKey),
            CancellationToken.None);

        Assert.Equal(CapturingMediator.CreatedOrderId, result.OrderId);
        Assert.Equal(1, fixture.Mediator.CreateOrderCallCount);
    }

    [Fact]
    public async Task Handle_FailsBeforeOrderCreation_WhenCartPriceIsStale()
    {
        var fixture = TestFixture.StockVariant(cartUnitPrice: 1700, currentUnitPrice: 1900);

        var ex = await Assert.ThrowsAsync<StoreDomainException>(() => fixture.Handler.Handle(
            Command(fixture.Cart.ModuleId),
            CancellationToken.None));

        Assert.Equal("CART_PRICE_CHANGED", ex.ErrorCode);
        Assert.Null(fixture.Mediator.CreateOrderCommand);
    }

    [Fact]
    public async Task Handle_FailsBeforeOrderCreation_WhenShopProductVariantIsUnavailable()
    {
        var fixture = TestFixture.StockVariant(cartUnitPrice: 1700, currentUnitPrice: 1700);
        fixture.PriceResolver.ExceptionToThrow = new StoreDomainException(
            "این تنوع در فروشگاه انتخاب‌شده فعال نیست.",
            "SHOP_PRODUCT_VARIANT_INACTIVE");

        var ex = await Assert.ThrowsAsync<StoreDomainException>(() => fixture.Handler.Handle(
            Command(fixture.Cart.ModuleId),
            CancellationToken.None));

        Assert.Equal("SHOP_PRODUCT_VARIANT_INACTIVE", ex.ErrorCode);
        Assert.Null(fixture.Mediator.CreateOrderCommand);
    }

    [Fact]
    public async Task Handle_FailsBeforeOrderCreation_WhenUsageDateIsRequiredButMissing()
    {
        var fixture = TestFixture.SessionVariantWithoutUsageDate();

        var ex = await Assert.ThrowsAsync<StoreDomainException>(() => fixture.Handler.Handle(
            Command(fixture.Cart.ModuleId),
            CancellationToken.None));

        Assert.Equal("USAGE_DATE_REQUIRED", ex.ErrorCode);
        Assert.Null(fixture.Mediator.CreateOrderCommand);
    }

    [Fact]
    public async Task Handle_FailsBeforeOrderCreation_WhenShopIsNotActive()
    {
        var fixture = TestFixture.StockVariant(cartUnitPrice: 1700, currentUnitPrice: 1700, activeShop: false);

        var ex = await Assert.ThrowsAsync<StoreDomainException>(() => fixture.Handler.Handle(
            Command(fixture.Cart.ModuleId),
            CancellationToken.None));

        Assert.Equal("SHOP_NOT_ACTIVE", ex.ErrorCode);
        Assert.Null(fixture.Mediator.CreateOrderCommand);
    }

    private static PlaceStoreOrderCommand Command(int moduleId, string? idempotencyKey = null)
        => new(
            UserId: UserId,
            ModuleId: moduleId,
            IdempotencyKey: idempotencyKey ?? Guid.NewGuid().ToString("N"));

    private sealed class TestFixture
    {
        private TestFixture(
            Cart cart,
            Guid shopProductVariantId,
            FakePriceResolver priceResolver,
            CapturingMediator mediator,
            FakeCartRepository cartRepository,
            PlaceStoreOrderCommandHandler handler)
        {
            Cart = cart;
            ShopProductVariantId = shopProductVariantId;
            PriceResolver = priceResolver;
            Mediator = mediator;
            CartRepository = cartRepository;
            Handler = handler;
        }

        public Cart Cart { get; }
        public Guid ShopProductVariantId { get; }
        public FakePriceResolver PriceResolver { get; }
        public CapturingMediator Mediator { get; }
        public FakeCartRepository CartRepository { get; }
        public PlaceStoreOrderCommandHandler Handler { get; }

        public static TestFixture StockVariant(long cartUnitPrice, long currentUnitPrice, bool activeShop = true)
        {
            var agreementProductId = Guid.NewGuid();
            var product = Product.Create(agreementProductId, "محصول تست", "test-product", stockCount: 10);
            var variant = product.AddVariant([], stockCount: 10, priceMinor: 2200, discountedPriceMinor: 1700, sku: "1cc");
            var cart = Cart.Create(UserId, moduleId: 1);
            cart.AddItem(ShopId, product.Id, variant.Id, sessionId: null, usageDate: null, quantity: 1, cartUnitPrice);

            return Create(
                cart,
                product,
                variant.Id,
                currentUnitPrice,
                originalPrice: 2200,
                salesModel: SalesModel.StockBased,
                activeShop);
        }

        public static TestFixture SessionVariantWithoutUsageDate()
        {
            var agreementProductId = Guid.NewGuid();
            var product = Product.Create(agreementProductId, "خدمت تست", "test-service", stockCount: 1);
            var variant = product.AddVariant(
                [],
                stockCount: 0,
                priceMinor: 18000,
                discountedPriceMinor: 15000,
                sku: "2cc",
                fromDate: new DateOnly(2026, 6, 22),
                toDate: new DateOnly(2026, 7, 21),
                capacityType: VariantCapacityType.PerEligibleDay,
                capacity: 10,
                salesModel: SalesModel.SessionBased);

            var cart = Cart.Create(UserId, moduleId: 1);
            cart.AddItem(ShopId, product.Id, variant.Id, sessionId: null, usageDate: null, quantity: 1, 15000);

            return Create(
                cart,
                product,
                variant.Id,
                currentUnitPrice: 15000,
                originalPrice: 18000,
                salesModel: SalesModel.SessionBased,
                activeShop: true);
        }

        private static TestFixture Create(
            Cart cart,
            Product product,
            Guid variantId,
            long currentUnitPrice,
            long originalPrice,
            SalesModel salesModel,
            bool activeShop)
        {
            var shop = Shop.Create("فروشگاه تست", "test-shop", ShopType.Online, Guid.NewGuid());
            if (activeShop)
                shop.Approve();

            var shopProductId = Guid.NewGuid();
            var shopProductVariantId = Guid.NewGuid();
            var priceResolver = new FakePriceResolver(new StoreResolvedPrice(
                UnitPriceMinor: currentUnitPrice,
                OriginalPriceMinor: originalPrice,
                DiscountedPriceMinor: currentUnitPrice == originalPrice ? null : currentUnitPrice,
                ShopProductId: shopProductId,
                ShopProductVariantId: shopProductVariantId,
                VariantId: variantId,
                Source: StorePriceSource.ShopProductVariant));

            var mediator = new CapturingMediator(product.AgreementProductId, salesModel);
            var cartRepository = new FakeCartRepository(cart);
            var handler = new PlaceStoreOrderCommandHandler(
                cartRepository,
                new FakeProductRepository(product),
                new FakeShopRepository(shop),
                new FakeProductSessionRepository(),
                priceResolver,
                new FreeDeliveryService(),
                mediator);

            return new TestFixture(cart, shopProductVariantId, priceResolver, mediator, cartRepository, handler);
        }
    }

    private sealed class FakeCartRepository : ICartRepository
    {
        private readonly Cart _cart;

        public FakeCartRepository(Cart cart) => _cart = cart;

        public int GetByUserAndModuleIdCallCount { get; private set; }

        public Task<Cart?> GetByUserAndModuleIdAsync(Guid userId, int moduleId, CancellationToken ct = default)
        {
            GetByUserAndModuleIdCallCount++;
            return Task.FromResult(_cart.UserId == userId && _cart.ModuleId == moduleId ? _cart : null);
        }

        public Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_cart.UserId == userId ? _cart : null);

        public Task<Cart?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_cart.Id == id ? _cart : null);

        public Task<Cart> AddItemAsync(Guid userId, int moduleId, Guid shopId, Guid productId, Guid? variantId, Guid? sessionId, DateOnly? usageDate, int quantity, long unitPriceMinor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddAsync(Cart cart, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Cart cart, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Cart cart, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly Product _product;

        public FakeProductRepository(Product product) => _product = product;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_product.Id == id ? _product : null);

        public Task<Product?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Product?>(null);
        public Task<Product?> GetDisplayableBySlugAsync(string slug, IReadOnlyList<Guid> allowedAgreementProductIds, CancellationToken ct = default) => Task.FromResult<Product?>(null);
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
        public Task UpdateAsync(Product product, CancellationToken ct = default) => Task.CompletedTask;
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

    private sealed class FakeProductSessionRepository : IProductSessionRepository
    {
        public Task<ProductSession?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ProductSession?>(null);
        public Task<List<ProductSession>> GetByProductIdAsync(Guid productId, CancellationToken ct = default) => Task.FromResult(new List<ProductSession>());
        public Task<List<ProductSession>> GetByProductIdAndDateAsync(Guid productId, DateOnly date, CancellationToken ct = default) => Task.FromResult(new List<ProductSession>());
        public Task<List<ProductSession>> GetAvailableByProductIdAsync(Guid productId, CancellationToken ct = default) => Task.FromResult(new List<ProductSession>());
        public Task UpdateAsync(ProductSession session, CancellationToken ct = default) => Task.CompletedTask;
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

    private sealed class FreeDeliveryService : IDeliveryService
    {
        public long CalcPrice(IReadOnlyList<DeliveryItemInput> items, Guid? shippingAddressId = null, Guid? shopId = null) => 0;
    }

    private sealed class CapturingMediator : IMediator
    {
        public static readonly Guid CreatedOrderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private readonly Guid _agreementProductId;
        private readonly SalesModel _salesModel;
        private readonly Dictionary<string, OrderDto> _ordersByIdempotencyKey = new(StringComparer.Ordinal);

        public CapturingMediator(Guid agreementProductId, SalesModel salesModel)
        {
            _agreementProductId = agreementProductId;
            _salesModel = salesModel;
        }

        public CreateOrderCommand? CreateOrderCommand { get; private set; }
        public int CreateOrderCallCount { get; private set; }

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
                    SalesModel: (short)_salesModel,
                    CommissionPercent: 0,
                    IsDeleted: false,
                    CreatedAt: DateTimeOffset.UtcNow),
                GetCategoryByIdQuery => new CategoryDto(1, "store", "store", "store", null, null, 0, true),
                GetStoreVariantSoldQuantityQuery => 0,
                GetUserAddressByIdQuery => new UserAddressDto(Guid.NewGuid(), UserId, "خانه", 1, 1, "نشانی", "1234567890", "کاربر", "09120000000", null, null, null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                GetOrderByIdempotencyKeyQuery query => _ordersByIdempotencyKey.GetValueOrDefault(query.IdempotencyKey),
                CreateOrderCommand command => Capture(command),
                _ => throw new NotSupportedException(request.GetType().FullName)
            };

            return Task.FromResult((TResponse)response!);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;

        private CreateOrderResponse Capture(CreateOrderCommand command)
        {
            CreateOrderCallCount++;
            CreateOrderCommand = command;
            var finalAmountMinor = command.Items.Sum(i => (i.UnitPriceMinor * i.Quantity) - i.DiscountAmountMinor)
                - command.DiscountCodeAmountMinor
                + command.ShippingFeeMinor;

            _ordersByIdempotencyKey[command.IdempotencyKey] = new OrderDto(
                Id: CreatedOrderId,
                OrderNumber: "ORD-STORE-1",
                UserId: command.UserId,
                TotalAmountMinor: command.Items.Sum(i => i.UnitPriceMinor * i.Quantity),
                DiscountAmountMinor: command.Items.Sum(i => i.DiscountAmountMinor),
                ShippingFeeMinor: command.ShippingFeeMinor,
                DiscountCode: command.DiscountCode,
                DiscountCodeAmountMinor: command.DiscountCodeAmountMinor,
                FinalAmountMinor: finalAmountMinor,
                Status: "Pending",
                PaymentState: "Unpaid",
                SourceModule: command.SourceModule,
                SourceReferenceId: command.SourceReferenceId,
                ReferenceType: command.ReferenceType ?? command.SourceModule,
                ShippingAddressId: command.ShippingAddressId,
                ShippingAddressSnapshotJson: command.ShippingAddressSnapshotJson,
                DeliveryDate: command.DeliveryDate,
                DeliveryTimeSlot: command.DeliveryTimeSlot,
                Items: command.Items.Select(i => new OrderItemDto(
                    Id: Guid.NewGuid(),
                    Title: i.Title,
                    UnitPriceMinor: i.UnitPriceMinor,
                    Quantity: i.Quantity,
                    FinalPriceMinor: (i.UnitPriceMinor * i.Quantity) - i.DiscountAmountMinor,
                    SourceItemId: i.SourceItemId,
                    CategoryCode: i.CategoryCode,
                    Tags: i.Tags,
                    MetadataJson: i.MetadataJson,
                    DeliveryMethod: i.DeliveryMethod)).ToList(),
                CreatedAt: DateTimeOffset.UtcNow);

            return new CreateOrderResponse(CreatedOrderId, "ORD-STORE-1", finalAmountMinor);
        }
    }
}
