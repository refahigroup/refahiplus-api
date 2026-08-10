using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;
using Xunit;
using MediatR;
using Refahi.Modules.Store.Application.Features.Vendor;
using Refahi.Modules.Store.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Store.Api.Endpoints.Checkout;
using Refahi.Modules.Store.Api.Security;

namespace Refahi.Modules.Store.Tests;

public sealed class UnifiedInPersonStoreOrderTests
{
    [Fact]
    public void CreateInPerson_PersistsSingleDeclaredGrossSnapshot()
    {
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var shopId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var order = StoreOrder.CreateInPerson(userId, actorId, "Vendor", shopId, supplierId,
            "vendor:key", new string('a', 64), Snapshot(shopId, supplierId, 1_250_000));

        var item = Assert.Single(order.Items);
        Assert.Equal(SalesChannel.InPerson, order.SalesChannel);
        Assert.Equal("Vendor", order.InitiatorType);
        Assert.Equal(actorId, order.CreatedByUserId);
        Assert.Equal(1, item.Quantity);
        Assert.Null(item.OfferId);
        Assert.Equal(1_250_000, item.DeclaredGrossAmountMinor);
        Assert.Equal(1_250_000, item.UnitPriceMinor);
        Assert.Equal(1_250_000, item.GrossAmountMinor);
        Assert.Equal(1_250_000, order.FinalAmountMinor);
        Assert.Equal(125_000, item.CommissionAmountMinor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateInPerson_RejectsNonPositiveDeclaredGross(long amount)
    {
        var ex = Assert.Throws<StoreDomainException>(() => StoreOrder.CreateInPerson(
            Guid.NewGuid(), Guid.NewGuid(), "User", Guid.NewGuid(), Guid.NewGuid(),
            "user:key", new string('b', 64), Snapshot(Guid.NewGuid(), Guid.NewGuid(), amount)));
        Assert.Contains(ex.ErrorCode, new[] { "INVALID_IN_PERSON_ITEM", "INVALID_STORE_ORDER_ITEM" });
    }

    [Fact]
    public void CreateInPerson_RejectsQuantityOtherThanOne()
    {
        var shopId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var invalid = Snapshot(shopId, supplierId, 10_000) with { Quantity = 2 };
        var ex = Assert.Throws<StoreDomainException>(() => StoreOrder.CreateInPerson(
            Guid.NewGuid(), Guid.NewGuid(), "User", shopId, supplierId,
            "user:key", new string('c', 64), invalid));
        Assert.Equal("INVALID_IN_PERSON_ITEM", ex.ErrorCode);
    }

    [Fact]
    public void OtpChallengeAndVerification_AreRetrySafe()
    {
        var shopId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var order = StoreOrder.CreateInPerson(Guid.NewGuid(), Guid.NewGuid(), "Vendor", shopId, supplierId,
            "vendor:key", new string('d', 64), Snapshot(shopId, supplierId, 100_000));
        var expires = DateTimeOffset.UtcNow.AddMinutes(2);

        order.BeginOtpDispatch();
        order.AttachOtpChallenge("protected-reference", expires);
        order.MarkOtpVerified();
        var verifiedAt = order.OtpVerifiedAt;
        order.MarkOtpVerified();

        Assert.Equal("protected-reference", order.OtpReferenceCode);
        Assert.Equal(expires, order.OtpExpiresAt);
        Assert.NotNull(order.OtpDispatchStartedAt);
        Assert.Equal(verifiedAt, order.OtpVerifiedAt);
    }

    [Fact]
    public void Lifecycle_UsesSharedStoreOrderTransitions()
    {
        var shopId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var order = StoreOrder.CreateInPerson(Guid.NewGuid(), Guid.NewGuid(), "User", shopId, supplierId,
            "user:key", new string('e', 64), Snapshot(shopId, supplierId, 100_000));
        var orderId = Guid.NewGuid();

        order.AttachOrder(orderId);
        order.MarkPaid();
        order.MarkPaid();
        order.MarkRefunded();
        order.MarkRefunded();

        Assert.Equal(orderId, order.OrderId);
        Assert.Equal(StoreOrderStatus.Refunded, order.Status);
    }

    [Fact]
    public void UserContract_DoesNotAcceptItemsOrOtp()
    {
        var properties = typeof(StartUserInPersonOrderCommand).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("Items", properties);
        Assert.DoesNotContain("OtpCode", properties);
        Assert.Equal(new[] { "UserId", "ShopId", "ProductId", "AmountMinor", "IdempotencyKey" }, properties);
    }

    [Fact]
    public void EfModel_AllowsNullableOfferAndPersistsAuditFields()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=test;Password=test")
            .Options;
        using var db = new StoreDbContext(options);
        var item = db.Model.FindEntityType(typeof(StoreOrderItem))!;
        var order = db.Model.FindEntityType(typeof(StoreOrder))!;

        Assert.True(item.FindProperty(nameof(StoreOrderItem.OfferId))!.IsNullable);
        Assert.NotNull(item.FindProperty(nameof(StoreOrderItem.DeclaredGrossAmountMinor)));
        Assert.NotNull(order.FindProperty(nameof(StoreOrder.CreatedByUserId)));
        Assert.NotNull(order.FindProperty(nameof(StoreOrder.OtpVerifiedAt)));
    }

    [Fact]
    public async Task VendorReadProjection_DistinguishesOnlineFromInPerson()
    {
        var actor = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var shop = Guid.NewGuid();
        var online = OnlineOrder(supplier, shop);
        var inPerson = InPersonOrder(actor, supplier, shop);
        var repository = new ReadRepository([online, inPerson]);
        var handler = new GetVendorStoreOrdersByOrderIdsHandler(repository,
            new ContextMediator(Context(actor, supplier, shop, StorePermissions.ViewOrders)));

        var result = await handler.Handle(new(actor, [online.OrderId!.Value, inPerson.OrderId!.Value]), default);

        Assert.Equal(2, result.Count);
        var onlineDto = result.Single(x => x.OrderId == online.OrderId);
        var inPersonDto = result.Single(x => x.OrderId == inPerson.OrderId);
        Assert.Equal("Online", onlineDto.SalesChannel);
        Assert.Equal("User", onlineDto.InitiatorType);
        Assert.Null(onlineDto.DeclaredGrossAmountMinor);
        Assert.NotNull(Assert.Single(onlineDto.Items).OfferId);
        Assert.Equal("InPerson", inPersonDto.SalesChannel);
        Assert.Equal("Vendor", inPersonDto.InitiatorType);
        Assert.Equal(750_000, inPersonDto.DeclaredGrossAmountMinor);
        Assert.Null(Assert.Single(inPersonDto.Items).OfferId);
    }

    [Fact]
    public async Task VendorReadProjection_HidesStoreOrderFromNonOwner()
    {
        var owner = Guid.NewGuid();
        var nonOwner = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var shop = Guid.NewGuid();
        var order = InPersonOrder(owner, supplier, shop);
        var repository = new ReadRepository([order]);
        var mediator = new ContextMediator(Context(nonOwner, supplier, shop, StorePermissions.ViewOwnOrders));
        var detail = new GetVendorStoreOrderByOrderIdHandler(repository, mediator);
        var batch = new GetVendorStoreOrdersByOrderIdsHandler(repository, mediator);

        Assert.Null(await detail.Handle(new(nonOwner, order.OrderId!.Value), default));
        Assert.Empty(await batch.Handle(new(nonOwner, [order.OrderId.Value]), default));
    }

    [Fact]
    public async Task VendorReadProjection_AllowsCashierOwnInPersonOrder()
    {
        var actor = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var shop = Guid.NewGuid();
        var order = InPersonOrder(actor, supplier, shop);
        var handler = new GetVendorStoreOrderByOrderIdHandler(new ReadRepository([order]),
            new ContextMediator(Context(actor, supplier, shop, StorePermissions.ViewOwnOrders)));

        var result = await handler.Handle(new(actor, order.OrderId!.Value), default);

        Assert.NotNull(result);
        Assert.Equal(order.Id, result.StoreOrderId);
        Assert.Equal(actor, result.CreatedByUserId);
    }

    [Fact]
    public async Task UserReadProjection_ReturnsMixedOnlineAndInPersonForOwner()
    {
        var owner = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var shop = Guid.NewGuid();
        var online = OnlineOrder(supplier, shop, owner);
        var inPerson = InPersonOrder(actor, supplier, shop, owner);
        var handler = new GetUserStoreOrdersByOrderIdsHandler(new ReadRepository([online, inPerson]));

        var result = await handler.Handle(new(owner, [online.OrderId!.Value, inPerson.OrderId!.Value]), default);

        Assert.Equal(new[] { "Online", "InPerson" }, result.Select(x => x.SalesChannel));
        Assert.Null(result[0].DeclaredGrossAmountMinor);
        Assert.Equal(750_000, result[1].DeclaredGrossAmountMinor);
    }

    [Fact]
    public async Task UserReadProjection_HidesNonOwnerAndAllowsExplicitAdmin()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var shop = Guid.NewGuid();
        var order = OnlineOrder(supplier, shop, owner);
        var repository = new ReadRepository([order]);
        var detail = new GetUserStoreOrderByOrderIdHandler(repository);
        var batch = new GetUserStoreOrdersByOrderIdsHandler(repository);

        Assert.Null(await detail.Handle(new(other, order.OrderId!.Value), default));
        Assert.Empty(await batch.Handle(new(other, [order.OrderId.Value]), default));
        Assert.NotNull(await detail.Handle(new(other, order.OrderId.Value, IsAdmin: true), default));
        Assert.Single(await batch.Handle(new(other, [order.OrderId.Value], IsAdmin: true), default));
    }

    [Fact]
    public void UserReadEndpoints_RequireUserOrAdminPolicy()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddScoped<InPersonTypedErrorFilter>();
        builder.Services.AddSingleton<IMediator>(new ContextMediator(Context(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), StorePermissions.ViewOrders)));
        var app = builder.Build();
        new UserStoreOrderReadEndpoints().Map(app.MapGroup("/api/store"));
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>().Where(x => x.RoutePattern.RawText?.Contains("store-orders") == true).ToArray();

        Assert.Equal(2, endpoints.Length);
        Assert.All(endpoints, endpoint => Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            metadata => metadata.Policy == "UserOrAdmin"));
    }

    private static StoreOrderItemSnapshot Snapshot(Guid shopId, Guid supplierId, long amount) => new(
        Guid.Empty, Guid.NewGuid(), null, null, null, "محصول حضوری", null, null,
        10, "store.service", supplierId, shopId, SalesChannel.InPerson, ProductType.Service,
        SalesModel.Unlimited, FulfillmentMethod.Pickup, 1, amount, 0, amount,
        Guid.NewGuid(), Guid.NewGuid(), 10m, null, 0, amount);

    private static StoreOrder InPersonOrder(Guid actor, Guid supplier, Guid shop, Guid? buyer = null)
    {
        var order = StoreOrder.CreateInPerson(buyer ?? Guid.NewGuid(), actor, "Vendor", shop, supplier,
            $"vendor:{actor:N}:key", new string('f', 64), Snapshot(shop, supplier, 750_000));
        order.AttachOrder(Guid.NewGuid());
        return order;
    }

    private static StoreOrder OnlineOrder(Guid supplier, Guid shop, Guid? user = null)
    {
        var item = new StoreOrderItemSnapshot(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(),
            "محصول آنلاین", null, null, 10, "store.goods", supplier, shop,
            SalesChannel.Online, ProductType.Goods, SalesModel.Unlimited, FulfillmentMethod.Shipping,
            2, 500_000, 10, 450_000, Guid.NewGuid(), Guid.NewGuid(), 10, null, 1);
        var order = StoreOrder.Create(user ?? Guid.NewGuid(), 1, shop, supplier, "online:key",
            new string('a', 64), [item], Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), 1);
        order.AttachOrder(Guid.NewGuid());
        return order;
    }

    private static StoreVendorContextDto Context(Guid actor, Guid supplier, Guid shop, string permission) => new(
        supplier, "Vendor", [], [], [new VendorShopAccessDto(shop, "Shop", "Active", "InPerson", null,
            [StoreAccessRoles.ShopCashier], [permission])]);

    private sealed class ReadRepository(IReadOnlyList<StoreOrder> values) : IStoreOrderRepository
    {
        public Task<StoreOrder?> GetByIdempotencyKeyAsync(Guid userId, string key, CancellationToken ct = default) =>
            Task.FromResult(values.SingleOrDefault(x => x.UserId == userId && x.IdempotencyKey == key));
        public Task<StoreOrder?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
            Task.FromResult(values.SingleOrDefault(x => x.OrderId == orderId));
        public Task<StoreOrder?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(values.SingleOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<StoreOrder>> GetByOrderIdsAsync(IReadOnlyCollection<Guid> ids,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StoreOrder>>(
                values.Where(x => x.OrderId.HasValue && ids.Contains(x.OrderId.Value)).ToArray());
        public Task AddAsync(StoreOrder order, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(StoreOrder order, CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitPaidAsync(Guid orderId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ContextMediator(StoreVendorContextDto context) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            object value = request switch
            {
                GetStoreVendorContextsQuery => new[] { context },
                _ => throw new NotSupportedException(request.GetType().Name)
            };
            return Task.FromResult((TResponse)value);
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,
            CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
