using Dapper;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Refahi.Modules.Pools.Api;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Infrastructure.Persistence;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Context;
using Xunit;

namespace Refahi.Modules.Pools.Tests;
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REFAHI_POOLS_TEST_CONNECTION"))) Skip = "Set REFAHI_POOLS_TEST_CONNECTION to an isolated PostgreSQL test database."; }
}
[CollectionDefinition("PoolsPostgres", DisableParallelization = true)]
public sealed class PoolsPostgresCollection;
[Collection("PoolsPostgres")]
public sealed class PoolPostgresTests
{
    private static string Connection => Environment.GetEnvironmentVariable("REFAHI_POOLS_TEST_CONNECTION")!;
    private static readonly PoolSelection Selection = new("poolticket", "1", "1", 1, 1);
    private static ServiceProvider Services(string scenario = "Success")
    {
        Assert.Contains("test", new NpgsqlConnectionStringBuilder(Connection).Database!, StringComparison.OrdinalIgnoreCase);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStringName"] = "Test", ["ConnectionStrings:Test"] = Connection,
            ["Pools:CatalogEnabled"] = "true", ["Pools:PurchaseEnabled"] = "true", ["Pools:MockLifecycleEnabled"] = "true",
            ["Pools:FixtureCatalogEnabled"] = "true", ["Pools:Mock:Scenario"] = scenario,
            ["Pools:TestFundsConfirmed"] = "true"
        }).Build();
        var s = new ServiceCollection(); s.AddLogging(); s.AddSingleton<IConfiguration>(config); s.AddSingleton<IHostEnvironment>(new TestEnvironment("Test"));
        s.RegisterPoolsModule(config);
        Refahi.Modules.Orders.Application.DI.RegisterApplication(s, config);
        Refahi.Modules.Orders.Infrastructure.DI.RegisterInfrastructure(s, config);
        Refahi.Modules.Wallets.Application.DI.RegisterApplication(s, config);
        Refahi.Modules.Wallets.Infrastructure.DI.RegisterInfrastructure(s, config);
        s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipeline<,>));
        return s.BuildServiceProvider();
    }
    private static async Task Migrate(ServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PoolsDbContext>().Database.MigrateAsync();
        await using var wallets = new WalletsDbContext(new DbContextOptionsBuilder<WalletsDbContext>().UseNpgsql(Connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).Options);
        await wallets.Database.MigrateAsync();
        // Existing Orders migration backfills historical Store owners; this isolated database has no Store orders.
        await using (var connection = new NpgsqlConnection(Connection))
            await connection.ExecuteAsync("create schema if not exists store; create table if not exists store.shops (\"Id\" uuid primary key, \"SupplierId\" uuid);");
        await using var orders = new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(Connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).Options);
        await orders.Database.MigrateAsync();
    }
    private static async Task<T> Send<T>(ServiceProvider services, IRequest<T> request)
    { using var scope = services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request); }
    private static async Task Reconcile(ServiceProvider services, Guid id)
    { using var scope = services.CreateScope(); await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ReconcilePoolBookingCommand(id)); }
    private static async Task<(Guid Org, Guid Personal)> SeedWallets(Guid user, string category = "entertainment")
    {
        var org = Guid.NewGuid(); var personal = Guid.NewGuid(); await using var c = new NpgsqlConnection(Connection);
        foreach (var pair in new[] { (org, 4), (personal, 2) })
            await c.ExecuteAsync("""
                insert into wallets.wallets (wallet_id,"OwnerId",wallet_type,status,currency,created_at,allowed_category_code,contract_expires_at)
                values (@Id,@User,@Kind,1,'IRR',@Now,@Category,@Expires);
                insert into wallets.wallet_balances (wallet_id,available_minor,pending_minor,currency,last_ledger_entry_id,version,updated_at)
                values (@Id,2000000,0,'IRR',null,1,@Now);
                """, new { Id = pair.Item1, User = user, Kind = (short)pair.Item2, Now = DateTimeOffset.UtcNow,
                    Category = pair.Item2 == 4 ? category : null, Expires = pair.Item2 == 4 ? DateTimeOffset.UtcNow.AddDays(10) : (DateTimeOffset?)null });
        return (org, personal);
    }
    private static async Task<PoolCheckout> ReserveAndCheckout(ServiceProvider services, Guid user)
    {
        var b = await Send(services, new ReservePoolBookingCommand(user, Selection, 1500000, Guid.NewGuid().ToString("N")));
        Assert.True(b.CanCheckout);
        return await Send(services, new CheckoutPoolBookingCommand(b.Id, user, b.Version, b.TotalMinor));
    }
    private static async Task PublishPaid(ServiceProvider services, PoolCheckout checkout, Guid user, Guid payment)
    {
        using var scope = services.CreateScope(); await scope.ServiceProvider.GetRequiredService<IMediator>().Publish(new OrderPaidIntegrationEvent(
            checkout.OrderId, checkout.OrderNumber, user, "Pools", checkout.BookingId, "PoolBooking", checkout.BookingId, payment, checkout.TotalMinor, DateTimeOffset.UtcNow));
    }
    [PostgresFact]
    public async Task Full_purchase_uses_real_orders_wallet_split_and_refunds_original_allocations()
    {
        await using var services = Services(); await Migrate(services); var user = Guid.NewGuid(); var wallets = await SeedWallets(user);
        var checkout = await ReserveAndCheckout(services, user);
        var pay = new PayOrderCommand(checkout.OrderId, user, "User", [new(wallets.Org, 600000), new(wallets.Personal, 900000)], "pay");
        var paid = await Send(services, pay); var replay = await Send(services, pay with { IdempotencyKey = "pay-again" }); Assert.Equal(paid.PaymentId, replay.PaymentId);
        await PublishPaid(services, checkout, user, paid.PaymentId); await PublishPaid(services, checkout, user, paid.PaymentId);
        var issued = await Send(services, new GetPoolBookingQuery(checkout.BookingId, user)); Assert.StartsWith("MOCK-TICKET-", issued.TicketCode); Assert.Equal("Issued", issued.IssuanceStatus);
        await using var c = new NpgsqlConnection(Connection);
        Assert.Equal(1400000, await c.ExecuteScalarAsync<long>("select available_minor from wallets.wallet_balances where wallet_id=@Id", new { Id = wallets.Org }));
        Assert.Equal(1100000, await c.ExecuteScalarAsync<long>("select available_minor from wallets.wallet_balances where wallet_id=@Id", new { Id = wallets.Personal }));
        var cancelled = await Send(services, new CancelPoolBookingCommand(checkout.BookingId, user)); Assert.Equal("Refunded", cancelled.PaymentStatus); Assert.Null(cancelled.TicketCode);
        await Send(services, new CancelPoolBookingCommand(checkout.BookingId, user));
        foreach (var id in new[] { wallets.Org, wallets.Personal }) Assert.Equal(2000000, await c.ExecuteScalarAsync<long>("select available_minor from wallets.wallet_balances where wallet_id=@Id", new { Id = id }));
    }
    [PostgresFact]
    public async Task Concurrent_reserve_and_checkout_return_one_booking_and_order()
    {
        await using var services = Services(); await Migrate(services); var user = Guid.NewGuid();
        var command = new ReservePoolBookingCommand(user, Selection, 1500000, Guid.NewGuid().ToString("N"));
        var bookings = await Task.WhenAll(Send(services, command), Send(services, command)); Assert.Equal(bookings[0].Id, bookings[1].Id);
        var b = bookings[0]; var checkout = new CheckoutPoolBookingCommand(b.Id, user, b.Version, b.TotalMinor);
        var orders = await Task.WhenAll(Send(services, checkout), Send(services, checkout)); Assert.Equal(orders[0].OrderId, orders[1].OrderId);
        await Assert.ThrowsAsync<PoolException>(() => Send(services, command with { Selection = Selection with { AdultCount = 2 } }));
        await Assert.ThrowsAsync<PoolException>(() => Send(services, new GetPoolBookingQuery(b.Id, Guid.NewGuid())));
    }
    [PostgresFact]
    public async Task Lost_reserve_confirm_and_cancel_responses_recover_after_new_scopes()
    {
        var user = Guid.NewGuid();
        await using var reserveServices = Services("ReserveAmbiguous"); await Migrate(reserveServices);
        var b = await Send(reserveServices, new ReservePoolBookingCommand(user, Selection, 1500000, Guid.NewGuid().ToString("N")));
        Assert.Equal("Reserving", b.ReservationStatus); await Reconcile(reserveServices, b.Id);
        b = await Send(reserveServices, new GetPoolBookingQuery(b.Id, user)); Assert.Equal("Reserved", b.ReservationStatus);
        var checkout = await Send(reserveServices, new CheckoutPoolBookingCommand(b.Id, user, b.Version, b.TotalMinor));
        var wallets = await SeedWallets(user);
        var paid = await Send(reserveServices, new PayOrderCommand(checkout.OrderId, user, "User", [new(wallets.Personal, 1500000)], "pay"));
        await using var confirmServices = Services("ConfirmAmbiguous"); await PublishPaid(confirmServices, checkout, user, paid.PaymentId);
        await Reconcile(confirmServices, b.Id); b = await Send(confirmServices, new GetPoolBookingQuery(b.Id, user)); Assert.Equal("Issued", b.IssuanceStatus);
        await using var cancelServices = Services("CancelAmbiguous");
        await Assert.ThrowsAsync<Refahi.Modules.Orders.Domain.Exceptions.OrderStateConflictException>(() => Send(cancelServices, new CancelPoolBookingCommand(b.Id, user)));
        await Reconcile(cancelServices, b.Id); b = await Send(cancelServices, new GetPoolBookingQuery(b.Id, user)); Assert.Equal("Refunded", b.PaymentStatus);
    }
    [PostgresFact]
    public async Task Crash_after_order_commit_repairs_attachment_and_expired_booking_cannot_debit()
    {
        await using var services = Services(); await Migrate(services); var user = Guid.NewGuid(); var checkout = await ReserveAndCheckout(services, user);
        using (var scope = services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PoolsDbContext>().Bookings.Where(x => x.Id == checkout.BookingId)
                .ExecuteUpdateAsync(x => x.SetProperty(b => b.OrderId, (Guid?)null).SetProperty(b => b.OrderNumber, (string?)null));
        await Reconcile(services, checkout.BookingId); var b = await Send(services, new GetPoolBookingQuery(checkout.BookingId, user)); Assert.Equal(checkout.OrderId, b.OrderId);
        using (var scope = services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PoolsDbContext>().Bookings.Where(x => x.Id == checkout.BookingId)
                .ExecuteUpdateAsync(x => x.SetProperty(b => b.PayableUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var wallets = await SeedWallets(user);
        await Assert.ThrowsAsync<PoolException>(() => Send(services, new PayOrderCommand(checkout.OrderId, user, "User", [new(wallets.Personal, 1500000)], "pay")));
        await Reconcile(services, checkout.BookingId);
        await using var c = new NpgsqlConnection(Connection);
        using (var scope = services.CreateScope())
            Assert.DoesNotContain(checkout.BookingId, await scope.ServiceProvider.GetRequiredService<IPoolBookingRepository>().CandidatesAsync(null, null, default));
        Assert.Equal(2000000, await c.ExecuteScalarAsync<long>("select available_minor from wallets.wallet_balances where wallet_id=@Id", new { Id = wallets.Personal }));
    }
    [PostgresFact]
    public async Task Issuance_failure_compensates_through_orders_and_disallowed_category_never_debits()
    {
        await using var services = Services("ConfirmFailure"); await Migrate(services); var user = Guid.NewGuid(); var wallets = await SeedWallets(user, "hotel");
        var checkout = await ReserveAndCheckout(services, user);
        await Assert.ThrowsAnyAsync<Exception>(() => Send(services, new PayOrderCommand(checkout.OrderId, user, "User", [new(wallets.Org, 1500000)], "blocked")));
        var paid = await Send(services, new PayOrderCommand(checkout.OrderId, user, "User", [new(wallets.Personal, 1500000)], "pay"));
        await PublishPaid(services, checkout, user, paid.PaymentId); await Reconcile(services, checkout.BookingId);
        var b = await Send(services, new GetPoolBookingQuery(checkout.BookingId, user)); Assert.Equal("Refunded", b.PaymentStatus);
    }
    [PostgresFact]
    public async Task Advisory_lock_cancels_waiter_and_uniqueness_is_enforced_by_postgres()
    {
        await using var services = Services(); await Migrate(services); var id = Guid.NewGuid(); using var scope = services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<IPoolMutationLock>();
        await using (var held = await gate.AcquireAsync(id, default))
        { using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.AcquireAsync(id, timeout.Token)); }
        await using var again = await gate.AcquireAsync(id, default);
        var db = scope.ServiceProvider.GetRequiredService<PoolsDbContext>(); var user = Guid.NewGuid();
        db.Bookings.Add(PoolBooking.Create(user, "poolticket", "same", "f", "{}", true)); await db.SaveChangesAsync();
        db.Bookings.Add(PoolBooking.Create(user, "poolticket", "same", "f", "{}", true)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [PostgresFact]
    public async Task Concurrent_payment_and_cancellation_never_leave_a_debit_or_duplicate_refund()
    {
        await using var services = Services(); await Migrate(services);
        for (var i = 0; i < 3; i++)
        {
            var user = Guid.NewGuid(); var wallets = await SeedWallets(user); var checkout = await ReserveAndCheckout(services, user);
            var pay = Task.Run(async () =>
            {
                try { await Send(services, new PayOrderCommand(checkout.OrderId, user, "User", [new(wallets.Personal, 1500000)], "race-pay")); }
                catch (Exception ex) when (ex is PoolException or Refahi.Modules.Orders.Domain.Exceptions.OrderStateConflictException) { }
            });
            await Task.WhenAll(pay, Send(services, new CancelPoolBookingCommand(checkout.BookingId, user)));
            await Reconcile(services, checkout.BookingId);
            var booking = await Send(services, new GetPoolBookingQuery(checkout.BookingId, user)); Assert.Equal("Cancelled", booking.CancellationStatus); Assert.Null(booking.TicketCode);
            await using var connection = new NpgsqlConnection(Connection);
            Assert.Equal(2000000, await connection.ExecuteScalarAsync<long>("select available_minor from wallets.wallet_balances where wallet_id=@Id", new { Id = wallets.Personal }));
        }
    }
    [PostgresFact]
    public async Task Forged_order_amount_category_and_deadline_are_rejected()
    {
        await using var services = Services(); await Migrate(services); var user = Guid.NewGuid();
        var b = await Send(services, new ReservePoolBookingCommand(user, Selection, 1500000, Guid.NewGuid().ToString("N")));
        var key = Refahi.Modules.Pools.Application.Features.Bookings.PoolBookingService.OrderKey(b.Id);
        var command = new CreateOrderCommand(user, "Pools", b.Id, [new("بلیط", b.TotalMinor + 1, 1, 0, b.Id, "entertainment.waterpark", null, null)],
            key, ReferenceType: "PoolBooking", SagaId: b.Id, PayableUntil: b.PayableUntil);
        await Assert.ThrowsAsync<PoolException>(() => Send(services, command));
        await Assert.ThrowsAsync<PoolException>(() => Send(services, command with { Items = [new("بلیط", b.TotalMinor, 1, 0, b.Id, "hotel", null, null)] }));
        await Assert.ThrowsAsync<PoolException>(() => Send(services, command with { Items = [new("بلیط", b.TotalMinor, 1, 0, b.Id, "entertainment.waterpark", null, null)], PayableUntil = b.PayableUntil!.Value.AddDays(1) }));
    }
    [PostgresFact]
    public async Task Catalog_identity_and_recovery_pagination_use_postgres_composite_keys()
    {
        await using var services = Services(); await Migrate(services); using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IPoolProviderFactory>(); var catalog = scope.ServiceProvider.GetRequiredService<IPoolCatalogStore>();
        var pool = (await factory.Get("poolticket").SearchAsync(new("poolticket"), default)).Items.Single();
        await catalog.UpsertAsync([pool, pool with { ProviderKey = "aabsar", Name = "آبسار" }], default);
        Assert.NotEqual((await catalog.GetAsync("poolticket", pool.Id, default))!.Name, (await catalog.GetAsync("aabsar", pool.Id, default))!.Name);
        Assert.Throws<PoolException>(() => factory.Get("aabsar"));
        var db = scope.ServiceProvider.GetRequiredService<PoolsDbContext>();
        for (var i = 0; i < 60; i++) db.Bookings.Add(PoolBooking.Create(Guid.NewGuid(), "poolticket", Guid.NewGuid().ToString("N"), "f", "{}", true));
        await db.SaveChangesAsync(); var repository = scope.ServiceProvider.GetRequiredService<IPoolBookingRepository>();
        var seen = new HashSet<Guid>(); DateTimeOffset? after = null; Guid? afterId = null;
        while (true)
        {
            var ids = await repository.CandidatesAsync(after, afterId, default); if (ids.Count == 0) break;
            Assert.All(ids, id => Assert.True(seen.Add(id))); var last = (await repository.GetAsync(ids[^1], default))!; after = last.CreatedAt; afterId = last.Id;
        }
        Assert.True(seen.Count >= 60);
    }
    [PostgresFact]
    public async Task Capacity_and_changed_price_do_not_create_payable_orders()
    {
        await using var capacity = Services("Capacity"); await Migrate(capacity); var user = Guid.NewGuid();
        var failed = await Send(capacity, new ReservePoolBookingCommand(user, Selection, 1500000, Guid.NewGuid().ToString("N")));
        Assert.Equal("Failed", failed.ReservationStatus); Assert.False(failed.CanCheckout); Assert.Null(failed.OrderId);
        await using var changed = Services("PriceChange");
        await Assert.ThrowsAsync<PoolException>(() => Send(changed, new ReservePoolBookingCommand(user, Selection, 1500000, Guid.NewGuid().ToString("N"))));
    }
    [PostgresFact]
    public async Task Http_api_validates_authorization_ownership_envelopes_and_no_store_checkout()
    {
        await using var services = Services(); await Migrate(services);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped<IServiceScope>(_ => services.CreateScope());
        builder.Services.AddScoped(sp => sp.GetRequiredService<IServiceScope>().ServiceProvider.GetRequiredService<IMediator>());
        builder.Services.AddAuthentication("PoolsTest").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("PoolsTest", _ => { });
        builder.Services.AddAuthorization(o => o.AddPolicy("UserOrAdmin", p => p.RequireAuthenticatedUser().RequireRole("User", "Admin")));
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        new Refahi.Modules.Pools.Api.Endpoints.PoolEndpoints().Map(app.MapGroup("/api/pools")); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        var catalog = await client.GetAsync("/api/pools/poolticket/pools"); Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
        using (var body = JsonDocument.Parse(await catalog.Content.ReadAsStringAsync())) Assert.True(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/pools/poolticket/pools?gender=2")).StatusCode);
        var request = new { Selection, ExpectedTotalMinor = 1500000, IdempotencyKey = Guid.NewGuid().ToString("N") };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/pools/bookings", request)).StatusCode);
        var user = Guid.NewGuid(); client.DefaultRequestHeaders.Add("X-Test-User", user.ToString());
        var reserved = await client.PostAsJsonAsync("/api/pools/bookings", request); Assert.Equal(HttpStatusCode.OK, reserved.StatusCode);
        Assert.True(reserved.Headers.CacheControl!.NoStore); Assert.True(reserved.Headers.CacheControl.Private);
        using var json = JsonDocument.Parse(await reserved.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data"); var id = data.GetProperty("id").GetGuid(); Assert.True(data.GetProperty("isMock").GetBoolean());
        client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/pools/bookings/{id}")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User", user.ToString());
        var checkout = await client.PostAsJsonAsync($"/api/pools/bookings/{id}/checkout", new { version = data.GetProperty("version").GetInt64(), totalMinor = 1500000 });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode); Assert.True(checkout.Headers.CacheControl!.NoStore);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/pools/bookings/{id}/cancel", new { })).StatusCode);
        await app.StopAsync();
    }
    public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"], out var user)) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.ToString()), new(ClaimTypes.Role, "User")], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
    public sealed class ValidationPipeline<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        {

            foreach (var validator in validators) await validator.ValidateAndThrowAsync(request, ct);
            return await next();
        }
    }
}


