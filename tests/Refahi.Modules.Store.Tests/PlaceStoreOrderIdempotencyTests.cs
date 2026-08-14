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
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class PlaceStoreOrderIdempotencyTests
{
    [Fact]
    public async Task Checkout_accepts_zero_final_price_offer_and_creates_zero_amount_order()
    {
        var fixture = Fixture.Create(discount: 100, fulfillment: FulfillmentMethod.Download);
        var result = await fixture.Handler.Handle(fixture.Command("free"), default);

        Assert.Equal(0, result.FinalAmountMinor);
        Assert.Equal(1, fixture.StoreOrders.AddCount);
        Assert.Equal(1, fixture.Mediator.CreateOrderCount);
    }

    [Theory]
    [InlineData(true, false, "SHIPPING_DETAILS_REQUIRED")]
    [InlineData(false, true, "DELIVERY_METHOD_REQUIRED")]
    public async Task Invalid_delivery_request_creates_neither_store_order_nor_order(
        bool omitShipping,
        bool omitDeliveryMethod,
        string expectedCode
    )
    {
        var fixture = Fixture.Create();
        var command = fixture.Command(
            "invalid",
            shippingAddressId: omitShipping ? null : Guid.NewGuid(),
            deliveryDate: omitShipping ? null : new DateOnly(2026, 8, 12),
            methods: omitDeliveryMethod ? null : fixture.ValidMethods()
        );

        var ex = await Assert.ThrowsAsync<StoreDomainException>(() =>
            fixture.Handler.Handle(command, default)
        );

        Assert.Equal(expectedCode, ex.ErrorCode);
        Assert.Equal(0, fixture.StoreOrders.AddCount);
        Assert.Equal(0, fixture.Mediator.CreateOrderCount);
    }

    [Fact]
    public async Task Same_payload_retries_exact_order_and_returns_standard_checkout_route()
    {
        var fixture = Fixture.Create();
        var command = fixture.Command(
            "same",
            Guid.NewGuid(),
            new DateOnly(2026, 8, 12),
            fixture.ValidMethods()
        );

        var first = await fixture.Handler.Handle(command, default);
        var second = await fixture.Handler.Handle(command, default);

        Assert.Equal(first.StoreOrderId, second.StoreOrderId);
        Assert.Equal(first.OrderId, second.OrderId);
        Assert.Equal($"/checkout/orders/{first.OrderId}", first.CheckoutDestination);
        Assert.Equal(first.CheckoutDestination, second.CheckoutDestination);
        Assert.Equal(1, fixture.StoreOrders.AddCount);
        Assert.Equal(1, fixture.Mediator.CreateOrderCount);
    }

    [Fact]
    public async Task Different_payload_after_attach_is_typed_conflict_without_order_mutation()
    {
        var fixture = Fixture.Create();
        var original = fixture.Command(
            "after",
            Guid.NewGuid(),
            new DateOnly(2026, 8, 12),
            fixture.ValidMethods()
        );
        await fixture.Handler.Handle(original, default);
        var changed = original with { DeliveryTimeSlot = 3 };

        var ex = await Assert.ThrowsAsync<StoreDomainException>(() =>
            fixture.Handler.Handle(changed, default)
        );

        Assert.Equal("IDEMPOTENCY_PAYLOAD_MISMATCH", ex.ErrorCode);
        Assert.Equal(1, fixture.Mediator.CreateOrderCount);
        Assert.Equal(0, fixture.Mediator.WalletCommandCount);
    }

    [Fact]
    public async Task Different_payload_before_attach_is_typed_conflict_without_order_mutation()
    {
        var fixture = Fixture.Create();
        var original = fixture.Command(
            "before",
            Guid.NewGuid(),
            new DateOnly(2026, 8, 12),
            fixture.ValidMethods()
        );
        fixture.StoreOrders.SeedPending(fixture.Context, original);
        var changed = original with { ShippingAddressId = Guid.NewGuid() };

        var ex = await Assert.ThrowsAsync<StoreDomainException>(() =>
            fixture.Handler.Handle(changed, default)
        );

        Assert.Equal("IDEMPOTENCY_PAYLOAD_MISMATCH", ex.ErrorCode);
        Assert.Equal(0, fixture.Mediator.CreateOrderCount);
        Assert.Equal(0, fixture.Mediator.WalletCommandCount);
    }

    private sealed class Fixture
    {
        public required Cart Cart { get; init; }
        public required OnlineOfferContext Context { get; init; }
        public required FakeStoreOrderRepository StoreOrders { get; init; }
        public required FakeMediator Mediator { get; init; }
        public required PlaceStoreOrderCommandHandler Handler { get; init; }

        public static Fixture Create(
            decimal discount = 10,
            FulfillmentMethod fulfillment = FulfillmentMethod.Shipping
        )
        {
            var supplierId = Guid.NewGuid();
            var product = Product.CreateCatalogProduct(
                supplierId,
                10,
                ProductType.Goods,
                SalesModel.Unlimited,
                fulfillment,
                "کالا",
                $"item-{Guid.NewGuid():N}"
            );
            var shop = Shop.Create("فروشگاه", "online-shop", ShopType.Online, supplierId);
            shop.Approve();
            var now = new DateTimeOffset(2026, 8, 9, 8, 0, 0, TimeSpan.Zero);
            var offer = Offer.Create(
                supplierId,
                product.Id,
                shop.Id,
                null,
                null,
                10_000,
                discount,
                now.AddDays(-1),
                null
            );
            offer.Activate();
            var cart = Cart.Create(Guid.NewGuid(), 4);
            cart.AddOfferItem(
                shop.Id,
                product.Id,
                offer.Id,
                null,
                null,
                null,
                2,
                offer.OriginalPriceMinor,
                offer.FinalPriceMinor
            );
            var term = new ResolvedAgreementCategoryTermDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                supplierId,
                10,
                10,
                1,
                5,
                now.AddDays(-2),
                now.AddDays(2)
            );
            var context = new OnlineOfferContext(offer, product, shop, term, null);
            var orders = new FakeStoreOrderRepository();
            var mediator = new FakeMediator();
            return new Fixture
            {
                Cart = cart,
                Context = context,
                StoreOrders = orders,
                Mediator = mediator,
                Handler = new PlaceStoreOrderCommandHandler(
                    new FakeCartRepository(cart),
                    orders,
                    new FakeOfferRepository(offer),
                    new FakeEligibility(context),
                    mediator,
                    new FixedTimeProvider(now)
                ),
            };
        }

        public Dictionary<Guid, short> ValidMethods() => new() { [Cart.Items.Single().Id] = 1 };

        public PlaceStoreOrderCommand Command(
            string key,
            Guid? shippingAddressId = null,
            DateOnly? deliveryDate = null,
            Dictionary<Guid, short>? methods = null
        ) => new(Cart.UserId, Cart.ModuleId, key, shippingAddressId, deliveryDate, 2, methods);
    }

    private sealed class FakeEligibility(OnlineOfferContext context)
        : IOnlineOfferEligibilityService
    {
        public Task<OnlineOfferContext> ResolveByIdAsync(
            Guid offerId,
            int quantity,
            Guid? variantId,
            Guid? sessionId,
            DateOnly? usageDate,
            CancellationToken ct
        ) => Task.FromResult(context);
    }

    private sealed class FakeCartRepository(Cart cart) : ICartRepository
    {
        public Task<Cart?> GetByUserAndModuleIdAsync(
            Guid userId,
            int moduleId,
            CancellationToken ct = default
        ) => Task.FromResult<Cart?>(cart);

        public Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<Cart?>(cart);

        public Task<Cart?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Cart?>(cart);

        public Task<Cart> AddItemAsync(
            Guid userId,
            int moduleId,
            Guid shopId,
            Guid productId,
            Guid? variantId,
            Guid? sessionId,
            DateOnly? usageDate,
            int quantity,
            long unitPriceMinor,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task AddAsync(Cart value, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Cart value, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Cart value, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeOfferRepository(Offer offer) : IOfferRepository
    {
        public Task<Offer?> ResolveAsync(
            Guid productId,
            Guid shopId,
            Guid? variantId,
            Guid? sessionId,
            DateTimeOffset atUtc,
            CancellationToken ct = default
        ) => Task.FromResult<Offer?>(offer);

        public Task<Offer?> GetByIdAsync(
            Guid id,
            bool includeDeleted = false,
            CancellationToken ct = default
        ) => Task.FromResult<Offer?>(offer);

        public Task<bool> HasOpenEndedCoordinateAsync(
            Guid productId,
            Guid shopId,
            Guid? variantId,
            Guid? sessionId,
            Guid? excludingId = null,
            CancellationToken ct = default
        ) => Task.FromResult(false);

        public Task<(IReadOnlyList<Offer> Items, int Total)> GetPagedAsync(
            Guid? productId,
            Guid? shopId,
            bool includeDeleted,
            DateTimeOffset? effectiveAtUtc,
            int page,
            int size,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<IReadOnlyList<OfferEligibilityCandidate>> GetEligibilityCandidatesAsync(
            Guid? productId,
            Guid? shopId,
            DateTimeOffset atUtc,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(IReadOnlyList<Offer> Items, int Total)> GetPageByIdsAsync(
            IReadOnlyCollection<Guid> eligibleIds,
            int page,
            int size,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task AddAsync(Offer value, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(
            Offer value,
            uint expectedVersion,
            CancellationToken ct = default
        ) => Task.CompletedTask;
    }

    private sealed class FakeStoreOrderRepository : IStoreOrderRepository
    {
        private StoreOrder? value;
        public int AddCount { get; private set; }

        public Task<StoreOrder?> GetByIdempotencyKeyAsync(
            Guid userId,
            string key,
            CancellationToken ct = default
        ) => Task.FromResult(value?.IdempotencyKey == key ? value : null);

        public Task<StoreOrder?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
            Task.FromResult(value?.OrderId == orderId ? value : null);

        public Task AddAsync(StoreOrder order, CancellationToken ct = default)
        {
            AddCount++;
            value = order;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(StoreOrder order, CancellationToken ct = default)
        {
            value = order;
            return Task.CompletedTask;
        }

        public Task CommitPaidAsync(Guid orderId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public void SeedPending(OnlineOfferContext context, PlaceStoreOrderCommand command)
        {
            var item = new StoreOrderItemSnapshot(
                Guid.NewGuid(),
                context.Product.Id,
                null,
                null,
                context.Offer.Id,
                context.Product.Title,
                null,
                null,
                context.Product.CategoryId,
                "store.test",
                context.Product.SupplierId,
                context.Shop.Id,
                SalesChannel.Online,
                context.Product.ProductType,
                context.Product.SalesModel,
                context.Product.FulfillmentMethod,
                1,
                context.Offer.OriginalPriceMinor,
                context.Offer.DiscountPercent,
                context.Offer.FinalPriceMinor,
                context.Term.AgreementId,
                context.Term.TermId,
                context.Term.CommissionPercent,
                null,
                1
            );
            value = StoreOrder.Create(
                command.UserId,
                command.ModuleId,
                context.Shop.Id,
                context.Product.SupplierId,
                command.IdempotencyKey,
                CheckoutRequestFingerprint.Create(command),
                [item],
                command.ShippingAddressId,
                command.DeliveryDate,
                command.DeliveryTimeSlot
            );
        }
    }

    private sealed class FakeMediator : IMediator
    {
        private CreateOrderResponse? created;
        public int CreateOrderCount { get; private set; }
        public int WalletCommandCount { get; private set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken ct = default
        )
        {
            object result = request switch
            {
                GetCategoryByIdQuery => new CategoryDto(
                    10,
                    "آزمایشی",
                    "test",
                    "store.test",
                    null,
                    null,
                    0,
                    true
                ),
                GetUserAddressByIdQuery query => new UserAddressDto(
                    query.AddressId,
                    query.UserId,
                    "خانه",
                    1,
                    1,
                    "نشانی",
                    "1234567890",
                    "گیرنده",
                    "09120000000",
                    null,
                    null,
                    null,
                    null,
                    true,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow
                ),
                CreateOrderCommand command => Create(command),
                GetOrderByIdempotencyKeyQuery => ToDto(),
                _ when request
                        .GetType()
                        .FullName?.Contains("Wallet", StringComparison.OrdinalIgnoreCase) == true =>
                    CountWallet(),
                _ => throw new NotSupportedException(request.GetType().FullName),
            };
            return Task.FromResult((TResponse)result);
        }

        private CreateOrderResponse Create(CreateOrderCommand command)
        {
            CreateOrderCount++;
            return created = new CreateOrderResponse(
                Guid.NewGuid(),
                "ORD-Canonical",
                command.Items.Sum(x => x.UnitPriceMinor * x.Quantity),
                "IRR"
            );
        }

        private OrderDto ToDto() =>
            new(
                created!.OrderId,
                created.OrderNumber,
                Guid.NewGuid(),
                created.FinalAmountMinor,
                0,
                0,
                null,
                0,
                created.FinalAmountMinor,
                "Pending",
                "Unpaid",
                "Store",
                Guid.NewGuid(),
                "StoreOrder",
                null,
                null,
                null,
                0,
                [],
                DateTimeOffset.UtcNow
            );

        private object CountWallet()
        {
            WalletCommandCount++;
            throw new InvalidOperationException();
        }

        public Task<object?> Send(object request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken ct = default
        )
            where TNotification : INotification => Task.CompletedTask;

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken ct = default
        ) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
