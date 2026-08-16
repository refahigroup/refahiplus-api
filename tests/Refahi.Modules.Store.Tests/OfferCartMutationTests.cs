using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;
using Refahi.Modules.Store.Application.Features.Cart.AddOfferToCart;
using Refahi.Modules.Store.Application.Features.Cart.GetOfferCart;
using Refahi.Modules.Store.Application.Features.Cart.OfferCart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class OfferCartMutationTests
{
    [Fact]
    public async Task Add_and_get_accept_a_one_hundred_percent_discount_offer()
    {
        var f = Fixture.Create(discount: 100);
        var add = new AddOfferToCartCommandHandler(f.Carts, f.Eligibility, f.Mediator);

        var addResult = await add.Handle(
            new AddOfferToCartCommand(f.UserId, f.ModuleId, f.Offer.Id, 1),
            default
        );
        var result = await new GetOfferCartQueryHandler(
            f.Carts,
            f.Offers,
            f.Products,
            f.Shops,
            f.Eligibility,
            f.Clock
        ).Handle(new GetOfferCartQuery(f.UserId, f.ModuleId), default);

        Assert.NotNull(result);
        Assert.Equal(0, addResult.Cart.SnapshotTotalMinor);
        Assert.False(addResult.Cart.HasOfferChanged);
        Assert.Equal(0, result!.SnapshotTotalMinor);
        Assert.Equal(0, result.CurrentTotalMinor);
        Assert.Equal(1, result.TotalItems);
        Assert.False(result.HasOfferChanged);
        Assert.Equal(f.Product.Title, result.Items.Single().ProductTitle);
        Assert.Equal(f.Product.Slug, result.Items.Single().ProductSlug);
        Assert.Equal(f.Shop.Name, result.Items.Single().ShopName);
        Assert.Equal("AVAILABLE", result.Items.Single().AvailabilityCode);
    }

    [Fact]
    public async Task Reconfirm_explicitly_refreshes_all_changed_offer_snapshots()
    {
        var f = Fixture.Create();
        f.SeedCart();
        f.Offer.Update(30_000, 20, f.Now.AddMinutes(-2), f.Now.AddDays(1));
        var result = await new ReconfirmOfferCartCommandHandler(
            f.Carts,
            f.Offers,
            f.Eligibility,
            f.Mediator,
            f.Clock
        ).Handle(new ReconfirmOfferCartCommand(f.UserId, f.ModuleId), default);

        Assert.Equal(24_000, result.Items.Single().SnapshotFinalUnitPriceMinor);
        Assert.False(result.HasOfferChanged);
        Assert.Equal(1, f.Carts.UpdateCount);
    }

    [Fact]
    public async Task Update_never_silently_accepts_offer_drift_and_explicit_confirmation_refreshes_snapshot()
    {
        var f = Fixture.Create();
        f.SeedCart();
        var item = f.Carts.Value!.Items.Single();
        f.Offer.Update(20_000, 10, f.Now.AddMinutes(-2), f.Now.AddDays(1));
        var handler = f.UpdateHandler();

        var conflict = await Assert.ThrowsAsync<CartOfferChangedException>(() =>
            handler.Handle(
                new UpdateOfferCartItemCommand(f.UserId, f.ModuleId, item.Id, 2),
                default
            )
        );
        Assert.Equal("OFFER_CHANGED", conflict.ErrorCode);
        Assert.Equal(0, f.Carts.UpdateCount);

        var result = await handler.Handle(
            new UpdateOfferCartItemCommand(
                f.UserId,
                f.ModuleId,
                item.Id,
                2,
                AcceptOfferChanges: true
            ),
            default
        );
        Assert.Equal(18_000, result.Items.Single().SnapshotFinalUnitPriceMinor);
        Assert.Equal(2, result.Items.Single().Quantity);
    }

    [Fact]
    public async Task Remove_is_owner_scoped_and_non_owner_cannot_discover_or_mutate_item()
    {
        var f = Fixture.Create();
        f.SeedCart();
        var itemId = f.Carts.Value!.Items.Single().Id;
        var handler = new RemoveOfferCartItemCommandHandler(f.Carts, f.Mediator);

        var denied = await Assert.ThrowsAsync<StoreDomainException>(() =>
            handler.Handle(
                new RemoveOfferCartItemCommand(Guid.NewGuid(), f.ModuleId, itemId),
                default
            )
        );
        Assert.Equal("CART_NOT_FOUND", denied.ErrorCode);
        Assert.Equal(0, f.Carts.UpdateCount);

        var result = await handler.Handle(
            new RemoveOfferCartItemCommand(f.UserId, f.ModuleId, itemId),
            default
        );
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Sync_deduplicates_input_and_same_idempotency_replay_does_not_increment_quantity()
    {
        var f = Fixture.Create();
        var handler = f.SyncHandler();
        var input = f.Input(quantity: 1);
        var command = new SyncOfferCartCommand(f.UserId, f.ModuleId, "sync-1", [input, input]);

        var first = await handler.Handle(command, default);
        var replay = await handler.Handle(command, default);

        Assert.Equal(2, first.Items.Single().Quantity);
        Assert.Equal(2, replay.Items.Single().Quantity);
        Assert.Equal(1, f.Carts.AddCount);

        var mismatch = await Assert.ThrowsAsync<StoreDomainException>(() =>
            handler.Handle(command with { Items = [f.Input(quantity: 3)] }, default)
        );
        Assert.Equal("IDEMPOTENCY_PAYLOAD_MISMATCH", mismatch.ErrorCode);
        Assert.Equal(2, f.Carts.Value!.Items.Single().Quantity);
    }

    [Fact]
    public async Task Sync_rejects_expired_offer_and_mixed_shop_without_partial_write()
    {
        var f = Fixture.Create();
        f.Offer.Deactivate();
        var expired = await Assert.ThrowsAsync<CartOfferChangedException>(() =>
            f.SyncHandler()
                .Handle(
                    new SyncOfferCartCommand(f.UserId, f.ModuleId, "expired", [f.Input()]),
                    default
                )
        );
        Assert.Equal("OFFER_CHANGED", expired.ErrorCode);
        Assert.Equal(0, f.Carts.AddCount);

        f.Offer.Activate();
        var other = Fixture.Create(userId: f.UserId, moduleId: f.ModuleId);
        f.Offers.Add(other.Offer);
        f.Eligibility.Add(other.Eligibility.Context);
        var mixed = await Assert.ThrowsAsync<StoreDomainException>(() =>
            f.SyncHandler()
                .Handle(
                    new SyncOfferCartCommand(
                        f.UserId,
                        f.ModuleId,
                        "mixed",
                        [f.Input(), other.Input()]
                    ),
                    default
                )
        );
        Assert.Equal("MIXED_SHOP_ITEMS", mixed.ErrorCode);
        Assert.Equal(0, f.Carts.AddCount);
    }

    [Fact]
    public async Task Update_propagates_cancellation_before_mutation()
    {
        var f = Fixture.Create();
        f.SeedCart();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            f.UpdateHandler()
                .Handle(
                    new UpdateOfferCartItemCommand(
                        f.UserId,
                        f.ModuleId,
                        f.Carts.Value!.Items.Single().Id,
                        1
                    ),
                    cts.Token
                )
        );
        Assert.Equal(0, f.Carts.UpdateCount);
    }

    private sealed class Fixture
    {
        public Guid UserId { get; init; }
        public int ModuleId { get; init; }
        public DateTimeOffset Now { get; init; }
        public required Product Product { get; init; }
        public required Shop Shop { get; init; }
        public required Offer Offer { get; init; }
        public required FakeCartRepository Carts { get; init; }
        public required FakeOfferRepository Offers { get; init; }
        public required FakeEligibility Eligibility { get; init; }
        public required FakeProductRepository Products { get; init; }
        public required FakeShopRepository Shops { get; init; }
        public required FakeMediator Mediator { get; init; }
        public required FixedTimeProvider Clock { get; init; }

        public static Fixture Create(decimal discount = 10, Guid? userId = null, int moduleId = 5)
        {
            var now = DateTimeOffset.UtcNow;
            var supplier = Guid.NewGuid();
            var product = Product.CreateCatalogProduct(
                supplier,
                11,
                ProductType.Goods,
                SalesModel.Unlimited,
                FulfillmentMethod.Download,
                "کالا",
                $"p-{Guid.NewGuid():N}"
            );
            var shop = Shop.Create("فروشگاه", $"s-{Guid.NewGuid():N}", ShopType.Online, supplier);
            shop.Approve();
            var offer = Offer.Create(
                supplier,
                product.Id,
                shop.Id,
                null,
                null,
                10_000,
                discount,
                now.AddDays(-1),
                now.AddDays(1)
            );
            offer.Activate();
            var term = new ResolvedAgreementCategoryTermDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                supplier,
                11,
                10,
                1,
                5,
                now.AddDays(-2),
                now.AddDays(2)
            );
            var offers = new FakeOfferRepository(offer);
            var carts = new FakeCartRepository();
            var eligibility = new FakeEligibility(
                new OnlineOfferContext(offer, product, shop, term, null)
            );
            var products = new FakeProductRepository(product);
            var shops = new FakeShopRepository(shop);
            var mediator = new FakeMediator(carts, offers, products, shops, eligibility);
            return new Fixture
            {
                UserId = userId ?? Guid.NewGuid(),
                ModuleId = moduleId,
                Now = now,
                Product = product,
                Shop = shop,
                Offer = offer,
                Carts = carts,
                Offers = offers,
                Eligibility = eligibility,
                Products = products,
                Shops = shops,
                Mediator = mediator,
                Clock = new FixedTimeProvider(now),
            };
        }

        public void SeedCart()
        {
            var cart = Cart.Create(UserId, ModuleId);
            cart.AddOfferItem(
                Shop.Id,
                Product.Id,
                Offer.Id,
                null,
                null,
                null,
                1,
                Offer.OriginalPriceMinor,
                Offer.FinalPriceMinor
            );
            Carts.Value = cart;
        }

        public SyncOfferCartItemInput Input(int quantity = 1) =>
            new(
                Offer.Id,
                quantity,
                null,
                null,
                null,
                Offer.OriginalPriceMinor,
                Offer.FinalPriceMinor
            );

        public UpdateOfferCartItemCommandHandler UpdateHandler() =>
            new(Carts, Offers, Eligibility, Mediator, Clock);

        public SyncOfferCartCommandHandler SyncHandler() =>
            new(
                Carts,
                Offers,
                Eligibility,
                Mediator,
                new MemoryCache(new MemoryCacheOptions()),
                Clock
            );
    }

    private sealed class FakeEligibility : IOnlineOfferEligibilityService
    {
        private readonly Dictionary<Guid, OnlineOfferContext> contexts;

        public FakeEligibility(OnlineOfferContext context)
        {
            Context = context;
            contexts = new() { [context.Offer.Id] = context };
        }

        public OnlineOfferContext Context { get; }

        public void Add(OnlineOfferContext context) => contexts[context.Offer.Id] = context;

        public Task<OnlineOfferContext> ResolveByIdAsync(
            Guid offerId,
            int quantity,
            Guid? variantId,
            Guid? sessionId,
            DateOnly? usageDate,
            CancellationToken ct
        )
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(contexts[offerId]);
        }
    }

    private sealed class FakeCartRepository : ICartRepository
    {
        public Cart? Value { get; set; }
        public int AddCount { get; private set; }
        public int UpdateCount { get; private set; }

        public Task<Cart?> GetByUserAndModuleIdAsync(
            Guid userId,
            int moduleId,
            CancellationToken ct = default
        )
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                Value?.UserId == userId && Value.ModuleId == moduleId ? Value : null
            );
        }

        public Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Value?.UserId == userId ? Value : null);

        public Task<Cart?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Value?.Id == id ? Value : null);

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

        public Task<Cart> AddOfferItemAsync(
            Guid userId,
            int moduleId,
            Guid shopId,
            Guid productId,
            Guid offerId,
            Guid? variantId,
            Guid? sessionId,
            DateOnly? usageDate,
            int quantity,
            long originalUnitPriceMinor,
            long finalUnitPriceMinor,
            CancellationToken ct = default
        )
        {
            Value ??= Cart.Create(userId, moduleId);
            Value.AddOfferItem(
                shopId,
                productId,
                offerId,
                variantId,
                sessionId,
                usageDate,
                quantity,
                originalUnitPriceMinor,
                finalUnitPriceMinor
            );
            return Task.FromResult(Value);
        }

        public Task<Cart> AddOfferItemsAsync(
            Guid userId,
            int moduleId,
            IReadOnlyList<OfferCartItemSpec> items,
            CancellationToken ct = default
        )
        {
            ct.ThrowIfCancellationRequested();
            var isNew = Value is null;
            Value ??= Cart.Create(userId, moduleId);
            foreach (var item in items)
                Value.AddOfferItem(
                    item.ShopId,
                    item.ProductId,
                    item.OfferId,
                    item.VariantId,
                    item.SessionId,
                    item.UsageDate,
                    item.Quantity,
                    item.OriginalUnitPriceMinor,
                    item.FinalUnitPriceMinor
                );
            if (isNew)
                AddCount++;
            else
                UpdateCount++;
            return Task.FromResult(Value);
        }

        public Task AddAsync(Cart cart, CancellationToken ct = default)
        {
            AddCount++;
            Value = cart;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Cart cart, CancellationToken ct = default)
        {
            UpdateCount++;
            Value = cart;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Cart cart, CancellationToken ct = default)
        {
            Value = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOfferRepository(Offer initial) : IOfferRepository
    {
        private readonly Dictionary<Guid, Offer> all = new() { [initial.Id] = initial };

        public void Add(Offer offer) => all[offer.Id] = offer;

        public Task<Offer?> ResolveAsync(
            Guid productId,
            Guid shopId,
            Guid? variantId,
            Guid? sessionId,
            DateTimeOffset atUtc,
            CancellationToken ct = default
        )
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<Offer?>(
                all.Values.FirstOrDefault(o =>
                    o.ProductId == productId
                    && o.ShopId == shopId
                    && o.ProductVariantId == variantId
                    && o.ProductSessionId == sessionId
                    && o.IsEffectiveAt(atUtc)
                )
            );
        }

        public Task<Offer?> GetByIdAsync(
            Guid id,
            bool includeDeleted = false,
            CancellationToken ct = default
        )
        {
            ct.ThrowIfCancellationRequested();
            all.TryGetValue(id, out var value);
            return Task.FromResult<Offer?>(value);
        }

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

    private sealed class FakeMediator(
        FakeCartRepository carts,
        FakeOfferRepository offers,
        FakeProductRepository products,
        FakeShopRepository shops,
        FakeEligibility eligibility
    ) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken ct = default
        )
        {
            if (request is GetOfferCartQuery query)
            {
                var dto = await new GetOfferCartQueryHandler(
                    carts,
                    offers,
                    products,
                    shops,
                    eligibility,
                    TimeProvider.System
                ).Handle(query, ct);
                return (TResponse)(object)dto!;
            }
            throw new NotSupportedException(request.GetType().FullName);
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

    private sealed class FakeProductRepository(Product product) : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Product?>(id == product.Id ? product : null);

        public Task<Product?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default) =>
            GetByIdAsync(id, ct);

        public Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult<Product?>(product.Slug == slug ? product : null);

        public Task<Product?> GetDisplayableBySlugAsync(
            string slug,
            IReadOnlyList<Guid> allowedAgreementProductIds,
            CancellationToken ct = default
        ) => GetBySlugAsync(slug, ct);

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

        public Task<List<Product>> GetCatalogEligibilityCandidatesAsync(
            Guid? supplierId,
            int? categoryId,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<(List<Product> Items, int Total)> GetCatalogPageByIdsAsync(
            IReadOnlyCollection<Guid> eligibleIds,
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
            IReadOnlyList<Guid> allowedAgreementProductIds,
            int page,
            int pageSize,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<List<Product>> GetByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) => Task.FromResult(ids.Contains(product.Id) ? new List<Product> { product } : []);

        public Task<List<Product>> GetByIdsForAdminWithDetailsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) => GetByIdsAsync(ids, ct);

        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(product.Slug == slug);

        public Task AddAsync(Product value, CancellationToken ct = default) => Task.CompletedTask;

        public Task AddVariantAttributeAsync(
            Product value,
            Refahi.Modules.Store.Domain.Entities.VariantAttribute attribute,
            CancellationToken ct = default
        ) => Task.CompletedTask;

        public Task AddVariantAttributeValueAsync(
            Product value,
            Refahi.Modules.Store.Domain.Entities.VariantAttributeValue item,
            CancellationToken ct = default
        ) => Task.CompletedTask;

        public Task AddProductVariantAsync(
            Product value,
            Refahi.Modules.Store.Domain.Entities.ProductVariant variant,
            CancellationToken ct = default
        ) => Task.CompletedTask;

        public Task UpdateAsync(Product value, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeShopRepository(Shop shop) : IShopRepository
    {
        public Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Shop?>(id == shop.Id ? shop : null);

        public Task<Shop?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult<Shop?>(shop.Slug == slug ? shop : null);

        public Task<Shop?> GetByProviderIdAsync(Guid providerId, CancellationToken ct = default) =>
            Task.FromResult<Shop?>(null);

        public Task<List<Shop>> GetBySupplierIdAsync(
            Guid supplierId,
            CancellationToken ct = default
        ) => Task.FromResult(shop.SupplierId == supplierId ? new List<Shop> { shop } : []);

        public Task<(List<Shop> Items, int Total)> GetPagedAsync(
            ShopType? shopType,
            ShopStatus? status,
            int page,
            int size,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(shop.Slug == slug);

        public Task<bool> ProviderHasShopAsync(Guid providerId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<(List<Shop> Items, int Total)> GetPagedByIdsAsync(
            IEnumerable<Guid> ids,
            int page,
            int size,
            CancellationToken ct = default
        ) =>
            Task.FromResult(
                (
                    ids.Contains(shop.Id) ? new List<Shop> { shop } : [],
                    ids.Contains(shop.Id) ? 1 : 0
                )
            );

        public Task<List<Shop>> GetByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) => Task.FromResult(ids.Contains(shop.Id) ? new List<Shop> { shop } : []);

        public Task AddAsync(Shop value, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Shop value, CancellationToken ct = default) => Task.CompletedTask;
    }
}
