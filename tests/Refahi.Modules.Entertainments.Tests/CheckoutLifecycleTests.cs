using MediatR;
using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Exceptions;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Requests;
using Refahi.Modules.Entertainments.Application.Features.Cart.GetCart;
using Refahi.Modules.Entertainments.Application.Features.Checkout.Sessions;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Infrastructure;
using Refahi.Modules.Entertainments.Infrastructure.Persistence;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Xunit;

namespace Refahi.Modules.Entertainments.Tests;

public sealed class CheckoutLifecycleTests
{
    [Fact]
    public async Task Reservation_review_changed_price_then_repeated_confirm_creates_one_central_order()
    {
        using var f = await Fixture.Create();
        var request = await f.Request();
        var session = await f.Service.Handle(request, default);
        Assert.Equal(220, session.TotalAmountMinor);
        Assert.Equal(session.Id, (await f.Service.Handle(request, default)).Id);
        Assert.Equal(1, f.Provider.Reservations);
        var result = await f.Service.Handle(new ConfirmEntertainmentSessionCommand(f.User, session.Id, session.Version), default);
        Assert.Equal("/checkout/orders/" + result.OrderId, result.CheckoutDestination);
        var replay = await f.Service.Handle(new ConfirmEntertainmentSessionCommand(f.User, session.Id, session.Version), default);
        Assert.Equal(result, replay);
        Assert.Single(f.Central.Created);
        var central = f.Central.Created.Values.Single();
        Assert.Equal("Entertainments", central.SourceModule); Assert.Equal("EntertainmentPurchase", central.ReferenceType);
        Assert.All(central.Items, x => Assert.Equal("entertainment.waterpark", x.CategoryCode));
        Assert.Empty((await f.Repository.GetCartAsync(f.User))!.Items);
        Assert.Single(await f.Db.Orders.ToListAsync());
        var recovery = new Refahi.Modules.Entertainments.Application.Features.Operations.ResolveEntertainmentReservationHandler(new EntertainmentSessionRepository(f.Db), new Gate(), new Protector());
        await Assert.ThrowsAsync<EntertainmentDomainException>(() => recovery.Handle(new(session.Id, Guid.NewGuid(), "notcreated", "cannot override an attached purchase", "operator-key"), default));
    }
    [Fact]
    public async Task Lost_create_order_response_is_recovered_with_the_same_purchase_and_central_key()
    {
        using var f = await Fixture.Create();
        var session = await f.Service.Handle(await f.Request(), default);
        f.Central.DropFirstCreateResponse = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => f.Service.Handle(new ConfirmEntertainmentSessionCommand(f.User, session.Id, session.Version), default));
        var result = await f.Service.Handle(new ConfirmEntertainmentSessionCommand(f.User, session.Id, session.Version), default);
        Assert.Single(f.Central.Created); Assert.Single(await f.Db.Orders.ToListAsync());
        Assert.Equal(f.Central.OrderId, result.OrderId);
    }
    [Fact]
    public async Task Ambiguous_reservation_is_replayed_as_unknown_without_second_external_call()
    {
        using var f = await Fixture.Create(); f.Provider.UnknownReservation = true;
        var request = await f.Request();
        await Assert.ThrowsAsync<EntertainmentProviderAmbiguousException>(() => f.Service.Handle(request, default));
        var replay = await f.Service.Handle(request, default);
        Assert.Equal("Unknown", replay.Status); Assert.Equal(1, f.Provider.Reservations);
        await Assert.ThrowsAsync<EntertainmentDomainException>(() => f.Service.Handle(new ConfirmEntertainmentSessionCommand(f.User, replay.Id, replay.Version), default));
        Assert.Empty(f.Central.Created);
    }
    [Fact]
    public async Task Changed_cart_or_wrong_owner_cannot_reserve_or_read_another_users_session()
    {
        using var f = await Fixture.Create(); var request = await f.Request();
        await Assert.ThrowsAsync<EntertainmentDomainException>(() => f.Service.Handle(request with { CartVersion = "old" }, default));
        Assert.Equal(0, f.Provider.Reservations);
        var session = await f.Service.Handle(request, default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.Handle(new GetEntertainmentSessionQuery(Guid.NewGuid(), session.Id), default));
        await Assert.ThrowsAsync<EntertainmentDomainException>(() => f.Service.Handle(request with { CartVersion = "changed" }, default));
    }
    [Fact]
    public async Task Financial_source_validation_rejects_a_changed_price_or_category()
    {
        using var f = await Fixture.Create();
        var session = await f.Service.Handle(await f.Request(), default);
        f.Central.DropFirstCreateResponse = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => f.Service.Handle(new ConfirmEntertainmentSessionCommand(f.User, session.Id, session.Version), default));
        var original = f.Central.Created.Values.Single();
        var validator = new Refahi.Modules.Entertainments.Application.Features.Checkout.EntertainmentOrderSourceValidator(f.Repository);
        await validator.Handle(new(original), default);
        await Assert.ThrowsAsync<EntertainmentDomainException>(() => validator.Handle(new(original with {
            Items = [original.Items.Single() with { UnitPriceMinor = 1 }] }), default));
        await Assert.ThrowsAsync<EntertainmentDomainException>(() => validator.Handle(new(original with {
            Items = [original.Items.Single() with { CategoryCode = "hotel" }] }), default));
    }
    [Fact]
    public async Task Manual_nonissuance_is_audited_idempotent_and_unlocks_orders_compensation()
    {
        using var f = await Fixture.Create();
        var session = await f.Service.Handle(await f.Request(), default);
        var result = await f.Service.Handle(new ConfirmEntertainmentSessionCommand(f.User, session.Id, session.Version), default);
        var purchase = await f.Repository.GetOrderAsync(result.EntertainmentPurchaseId);
        var fulfillment = purchase!.Fulfillments.Single();
        var previous = fulfillment.BeginAttempt("fulfill", "stable-issue", "hash"); previous.Fail("unknown", true);
        fulfillment.Fail("unknown", true); purchase.RequireManualReview(); await f.Repository.SaveChangesAsync();
        var resolver = new Refahi.Modules.Entertainments.Application.Features.Operations.ResolveOperation.ResolveEntertainmentOperationCommandHandler(f.Repository, new Protector(), new Gate());
        var request = new Refahi.Modules.Entertainments.Application.Features.Operations.ResolveOperation.ResolveEntertainmentOperationCommand(
            fulfillment.Id, "notcreated", "Provider confirmed no invoice exists", null, null, Guid.NewGuid(), "operator-key");
        await resolver.Handle(request, default); await resolver.Handle(request, default);
        purchase = await f.Repository.GetOrderAsync(result.EntertainmentPurchaseId);
        Assert.Equal(EntertainmentPurchaseStatus.CompensationPending, purchase!.Status);
        var manual = purchase.Fulfillments.Single().Attempts.Single(x => x.Operation == "manual-resolution");
        Assert.Equal(request.ActorId, manual.ActorId); Assert.NotEmpty(manual.EvidenceProtected!);
        Assert.DoesNotContain(purchase.Fulfillments.Single().Attempts, x => x.Operation == "fulfill" && x.Outcome == ProviderOperationOutcome.Ambiguous);
        var participant = new Refahi.Modules.Entertainments.Application.Features.Checkout.CancellationParticipant.EntertainmentCancellationParticipant(f.Repository, new EntertainmentProviderFactory([f.Provider]), new Gate());
        var prepared = await participant.PrepareAsync(new(result.OrderId, f.User, "Entertainments", purchase.Id, "EntertainmentPurchase", "Paid", "compensation"), default);
        Assert.True(prepared.CanContinue);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Paid_event_or_recovery_sweep_issues_once_and_persists_all_tickets(bool lostEvent)
    {
        using var f = await Fixture.Create();
        var session = await f.Service.Handle(await f.Request(), default);
        var result = await f.Service.Handle(new ConfirmEntertainmentSessionCommand(f.User, session.Id, session.Version), default);
        f.Central.Paid = true;
        if (!lostEvent)
        {
            var handler = new Refahi.Modules.Entertainments.Application.Features.Checkout.EntertainmentPurchasePaidNotificationHandler(f.Repository, new Gate());
            var notification = new Refahi.Modules.Orders.Application.Contracts.IntegrationEvents.OrderPaidIntegrationEvent(result.OrderId, "TEST", f.User, "Entertainments", result.EntertainmentPurchaseId, "EntertainmentPurchase", null, Guid.NewGuid(), 220, DateTimeOffset.UtcNow);
            await handler.Handle(notification, default); await handler.Handle(notification, default);
        }
        var processor = f.Processor(); await processor.ProcessBatchAsync(default); await processor.ProcessBatchAsync(default);
        var purchase = await f.Repository.GetOrderAsync(result.EntertainmentPurchaseId);
        Assert.Equal(EntertainmentPurchaseStatus.Completed, purchase!.Status);
        Assert.Equal(2, purchase.Fulfillments.Single().Tickets.Count);
        Assert.Equal(1, f.Provider.FulfillCalls); Assert.Single(f.Central.Created);
        var reader = new Refahi.Modules.Entertainments.Application.Features.Order.GetOrder.GetEntertainmentPurchaseQueryHandler(f.Repository, new Protector(), new EntertainmentProviderFactory([]));
        var historical = await reader.Handle(new(f.User, "User", purchase.Id, true), default);
        Assert.Equal(2, historical!.Fulfillments.Single().Tickets.Count); Assert.False(historical.CanCancel);
    }
    [Fact]
    public async Task Unknown_issuance_is_queried_without_reissuing_or_refunding()
    {
        using var f = await Fixture.Create(); var session = await f.Service.Handle(await f.Request(), default);
        var result = await f.Service.Handle(new ConfirmEntertainmentSessionCommand(f.User, session.Id, session.Version), default);
        f.Central.Paid = true; f.Provider.UnknownIssuance = true;
        var processor = f.Processor(); await processor.ProcessBatchAsync(default);
        var purchase = await f.Repository.GetOrderAsync(result.EntertainmentPurchaseId);
        Assert.Equal(EntertainmentPurchaseStatus.AwaitingResult, purchase!.Status);
        f.Db.Entry(purchase.Fulfillments.Single()).Property("NextCheckAt").CurrentValue = DateTimeOffset.UtcNow.AddSeconds(-1);
        await f.Db.SaveChangesAsync(); await processor.ProcessBatchAsync(default);
        purchase = await f.Repository.GetOrderAsync(purchase.Id);
        Assert.Equal(EntertainmentPurchaseStatus.ManualReview, purchase!.Status);
        Assert.Equal(1, f.Provider.FulfillCalls); Assert.Equal(1, f.Provider.StatusCalls); Assert.Equal(0, f.Central.Cancellations);
    }
    [Fact]
    public async Task Unknown_unpaid_reservation_can_only_restart_after_audited_operator_resolution()
    {
        using var f = await Fixture.Create(); f.Provider.UnknownReservation = true;
        var request = await f.Request();
        await Assert.ThrowsAsync<EntertainmentProviderAmbiguousException>(() => f.Service.Handle(request, default));
        var stored = (await f.Service.Handle(new FindEntertainmentSessionQuery(f.User, request.IdempotencyKey), default))!;
        var handler = new Refahi.Modules.Entertainments.Application.Features.Operations.ResolveEntertainmentReservationHandler(new EntertainmentSessionRepository(f.Db), new Gate(), new Protector());
        var resolution = new Refahi.Modules.Entertainments.Application.Features.Operations.ResolveEntertainmentReservationCommand(stored.Id, Guid.NewGuid(), "expired", "Provider confirmed no active reservation remains", "operator-key");
        await handler.Handle(resolution, default); await handler.Handle(resolution, default);
        Assert.Equal("Released", (await f.Service.Handle(new GetEntertainmentSessionQuery(f.User, stored.Id), default)).Status);
        await Assert.ThrowsAsync<EntertainmentDomainException>(() => handler.Handle(resolution with { Evidence = "changed" }, default));
        f.Provider.UnknownReservation = false;
        var next = await f.Service.Handle(request with { IdempotencyKey = "new-reviewed-attempt" }, default);
        Assert.Equal("Reserved", next.Status); Assert.NotEqual(stored.Id, next.Id); Assert.Equal(2, f.Provider.Reservations);
    }
    private sealed class Retention : IEntertainmentPrivateDataRetention
    { public Task PruneAsync(DateTimeOffset cutoff, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Fixture : IDisposable
    {
        public readonly Guid User = Guid.NewGuid();
        public readonly EntertainmentDbContext Db = new(new DbContextOptionsBuilder<EntertainmentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly Provider Provider = new(); public readonly CentralOrders Central = new();
        public EntertainmentRepository Repository = null!; public EntertainmentSessionService Service = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); f.Repository = new(f.Db);
            var cart = EntertainmentCart.Create(f.User);
            cart.AddOrReplace(new("touristpanel", "supplier", Guid.NewGuid().ToString(), "offer", "option", "خدمت", "مجموعه", "خدمت", null, "بلیت", "سانس", 2, 100, 100));
            await f.Repository.AddCartAsync(cart); await f.Repository.SaveChangesAsync();
            f.Service = new(new EntertainmentSessionRepository(f.Db), f.Repository, new EntertainmentProviderFactory([f.Provider]), new Protector(), new Gate(), f.Central);
            return f;
        }
        public async Task<StartEntertainmentSessionCommand> Request()
        {
            var cart = await new GetEntertainmentCartQueryHandler(Repository).Handle(new(User), default);
            return new(User, "touristpanel", "stable", [new(cart.Items.Single().Id, [new("one", null), new("two", null)])], cart.CheckoutVersions["touristpanel"]);
        }
        public Refahi.Modules.Entertainments.Application.EntertainmentIssuanceProcessor Processor() => new(Repository,
            new EntertainmentProviderFactory([Provider]), new Protector(), Central,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Refahi.Modules.Entertainments.Application.EntertainmentIssuanceProcessor>.Instance,
            Microsoft.Extensions.Options.Options.Create(new Refahi.Modules.Entertainments.Application.EntertainmentRuntimeOptions()),
            new Gate(), new EntertainmentSessionRepository(Db), new Retention());
        public void Dispose() => Db.Dispose();
    }
    private sealed class Protector : IEntertainmentSecretProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => value;
        public string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    }
    private sealed class Gate : IEntertainmentMutationLock
    {
        public Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct) => Task.FromResult<IAsyncDisposable>(new Handle());
        private sealed class Handle : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }
    private sealed class Provider : IEntertainmentProvider, IEntertainmentReservationProvider, IEntertainmentFulfillmentStatusProvider
    {
        public int Reservations; public bool UnknownReservation; public bool UnknownIssuance; public int FulfillCalls; public int StatusCalls; private int _ticketCount;
        public string Key => "touristpanel"; public string Name => "توریست‌پنل";
        public EntertainmentProviderCapabilities Capabilities => new(false, false);
        public Task<EntertainmentReservationResult> ReserveAsync(EntertainmentReservationRequest request, CancellationToken ct)
        {
            Reservations++;
            if (UnknownReservation) throw new EntertainmentProviderAmbiguousException("unknown");
            return Task.FromResult(new EntertainmentReservationResult("external", DateTimeOffset.UtcNow.AddMinutes(15),
                request.Lines.Select(x => new EntertainmentReservedLine(x.CartItemId,
                    new(Key,"supplier",x.ProgramKey,x.OfferKey,x.PurchaseOptionKey,"مجموعه","خدمت",null,"سانس","بلیت","entertainment.waterpark",x.Quantity,110,110,10,"{}"),x.Passengers)).ToArray(),"context"));
        }
        public Task ReleaseAsync(string reference, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<EntertainmentSupplierDto>> GetSuppliersAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<EntertainmentSupplierDto?> GetSupplierAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<EntertainmentCatalogPage> GetProgramsAsync(EntertainmentCatalogQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<EntertainmentProgramDto?> GetProgramAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<EntertainmentQuoteResult> QuoteAsync(EntertainmentQuoteRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<EntertainmentFulfillmentResult> FulfillAsync(EntertainmentFulfillmentRequest request, CancellationToken ct)
        {
            FulfillCalls++; _ticketCount = request.Lines.Sum(x => x.Quantity);
            if (UnknownIssuance) throw new EntertainmentProviderAmbiguousException("response lost");
            return Task.FromResult(Completed());
        }
        private EntertainmentFulfillmentResult Completed() => new("invoice", Enumerable.Range(0, _ticketCount).Select(x => new EntertainmentIssuedTicket("ticket-"+x, false)).ToArray());
        public Task<EntertainmentFulfillmentResult> GetStatusAsync(string operation, string? invoice, CancellationToken ct)
        { StatusCalls++; return Task.FromResult(UnknownIssuance ? new EntertainmentFulfillmentResult("", []) { Status = "ManualReview" } : Completed()); }
        public Task CancelAsync(EntertainmentCancellationRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class CentralOrders : IMediator
    {
        public readonly Dictionary<string, CreateOrderCommand> Created = new();
        public readonly Guid OrderId = Guid.NewGuid(); public bool DropFirstCreateResponse; public bool Paid; public int Cancellations;
        public Task<T> Send<T>(IRequest<T> request, CancellationToken ct = default)
        {
            object result;
            if (request is GetUserContactQuery contact) result = new UserContactDto(contact.UserId, "کاربر آزمون", "09120000000");
            else if (request is CreateOrderCommand create)
            {
                Created.TryAdd(create.IdempotencyKey, create);
                if (DropFirstCreateResponse) { DropFirstCreateResponse = false; throw new HttpRequestException("response lost after central commit"); }
                result = new CreateOrderResponse(OrderId, "TEST", create.Items.Sum(x => x.UnitPriceMinor * x.Quantity));
            }
            else if (request is GetOrderByIdQuery query)
            {
                var original = Created.Values.Single(); var total = original.Items.Sum(x => x.UnitPriceMinor * x.Quantity);
                result = new OrderDto(OrderId,"TEST",query.CallerUserId,total,0,0,null,0,total,"Created",Paid ? "Paid" : "Unpaid",original.SourceModule,
                    original.SourceReferenceId,original.ReferenceType!,null,null,null,0,
                    original.Items.Select(x => new OrderItemDto(Guid.NewGuid(), x.Title, x.UnitPriceMinor, x.Quantity, x.UnitPriceMinor*x.Quantity, x.SourceItemId, x.CategoryCode, x.Tags, x.MetadataJson, 0)).ToList(),DateTimeOffset.UtcNow);
            }
            else if (request is CancelOrderCommand cancel) { Cancellations++; result = new CancelOrderResponse(cancel.OrderId, "Cancelled", "Refunded"); }
            else throw new InvalidOperationException("Unexpected module call: " + request.GetType().Name);
            return Task.FromResult((T)result);
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<T>(T notification, CancellationToken ct = default) where T : INotification => Task.CompletedTask;
        public IAsyncEnumerable<T> CreateStream<T>(IStreamRequest<T> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
