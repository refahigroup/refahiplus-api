using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
using Refahi.Modules.Entertainments.Application.Features.Discovery;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Infrastructure.Persistence;
using Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel;
using Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel.Contracts;
using Refahi.Modules.References.Application.Contracts.Queries;
using Xunit;
using static Refahi.Modules.Entertainments.Tests.TouristPanelClientTests;

namespace Refahi.Modules.Entertainments.Tests;

public sealed class DisposablePostgresFactAttribute : FactAttribute
{
    public DisposablePostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REFAHI_ENTERTAINMENTS_TEST_CONNECTION")))
            Skip = "Requires an explicitly configured disposable PostgreSQL test database.";
    }
}
public sealed class DiscoveryPostgresTests
{
    [DisposablePostgresFact]
    public async Task Upgrade_sync_identity_search_freshness_and_provider_payload_ownership()
    {
        var connection = Environment.GetEnvironmentVariable("REFAHI_ENTERTAINMENTS_TEST_CONNECTION")!;
        await Refahi.Tests.DisposablePostgres.ValidateAndProbeAsync(connection, "entertainments_test", 55442);
        await using var db = new EntertainmentDbContext(new DbContextOptionsBuilder<EntertainmentDbContext>()
            .UseNpgsql(connection, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "entertainments")).Options);
        // This test owns only the explicitly named disposable database; never the application's DB.
        await db.Database.EnsureDeletedAsync();
        var migrator = db.GetService<IMigrator>();
        var initial = (await db.Database.GetPendingMigrationsAsync()).First();
        await migrator.MigrateAsync(initial);
        var settings = new TouristPanelOptions { Enabled = true, Tenant = Guid.NewGuid().ToString(), CatalogMaxPages = 4 };
        var native = Guid.NewGuid().ToString(); var supplier = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var old = TouristPanelProgram.Create(settings.AccountKey, native);
        old.List(supplier, "مجموعه", "ارکیده", 1, true, "tp-tehran", "تهران", 7, "تفریح", now);
        old.Publish("ارکیده", "شرح پیشین", null, "پیروزی", false, false, [], "ارکیده پیروزی", now, 15);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO entertainments.touristpanel_programs
            ("ProgramKey","AccountKey","NativeId","SupplierId","SupplierTitle","Title","Description","Address","LocationCode","NativeCityName","CategoryCode","CategoryTitle","ProgramType","RequiresManifest","RequiresIdentityNumber","Images","SearchText","IsListed","ListedAtUtc","ObservedAtUtc","NextDetailAttemptUtc")
            VALUES ({old.ProgramKey},{old.AccountKey},{old.NativeId},{old.SupplierId},{old.SupplierTitle},{old.Title},{old.Description},{old.Address},{old.LocationCode},{old.NativeCityName},{old.CategoryCode},{old.CategoryTitle},{old.ProgramType},false,false,{old.Images},{old.SearchText},true,{now},{now},{now})
            """);
        var key = old.ProgramKey;
        var legacySessionId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO entertainments.checkout_sessions
                ("Id","UserId","ProviderKey","IdempotencyKey","Fingerprint","RequestProtected","ReservationProtected","ReservationReference","Status","CreatedAt","PayableUntil")
            VALUES ({legacySessionId},{Guid.NewGuid()},{"touristpanel"},{"legacy-test"},{"fingerprint"},{"private-request"},{"private-reservation"},{"external-reference"},2,{now},{now.AddMinutes(15)})
            """);
        await migrator.MigrateAsync();
        var preservedSession = await new EntertainmentSessionRepository(db).GetAsync(legacySessionId, default);
        Assert.Equal("private-reservation", preservedSession!.ReservationProtected);
        Assert.Equal("external-reference", preservedSession.ReservationReference);
        Assert.Equal(key, (await db.Programs.SingleAsync()).ProgramKey);
        var state = TouristPanelSyncState.Create(settings.AccountKey); state.Success(now, 15); db.SyncStates.Add(state);
        db.LocationMappings.Add(TouristPanelLocationMapping.Create(settings.AccountKey, "tp-tehran", 10, Guid.NewGuid()));
        await db.SaveChangesAsync();
        var geography = new Geography();
        var reader = new EntertainmentDiscoveryReader(db, geography, Options.Create(settings), Options.Create(new EntertainmentsOptions()));
        var result = await reader.SearchAsync(new("اركيده پيروزي", 1, 10), 20, default);
        Assert.Single(result.Items); Assert.False(result.IsPartial); Assert.Equal(key, result.Items[0].ProgramKey);
        Assert.Empty((await reader.SearchAsync(new(null, 2, null), 20, default)).Items);
        var catalogue = await new EntertainmentsCatalogueSource(reader).SearchAsync(new(Q: "ارکیده", Location: "nearby", Latitude: 35.7, Longitude: 51.4), 20, default);
        var item = Assert.Single(catalogue.Items);
        Assert.Equal($"entertainments:{key:N}", item.Id); Assert.Null(item.Price);
        Assert.Null(CatalogueOrdering.Prepare(item, new(Location: "nearby", Latitude: 35.7, Longitude: 51.4)).DistanceMeters);
        Assert.Single(await reader.CitiesAsync(default));
        var publicSupplier = await db.Suppliers.SingleAsync();
        Assert.NotEqual(Guid.Empty, publicSupplier.SupplierKey);
        var programs = await reader.ProgramsAsync(null, publicSupplier.SupplierKey.ToString("N"), 1, 20, null, null, default);
        Assert.Equal(key.ToString("N"), Assert.Single(programs.Items).ProgramKey);
        Assert.Equal(publicSupplier.SupplierKey.ToString("N"), programs.Items[0].SupplierKey);
        Assert.Equal("Undated", programs.Items[0].SchedulingMode);
        Assert.Single((await reader.SuppliersAsync(1, 20, null, null, default)).Items);
        Assert.NotNull(await reader.SupplierAsync(publicSupplier.SupplierKey, default));
        Assert.Single((await reader.FiltersAsync(default)).Categories);

        Assert.Equal(native, await reader.ResolveNativeAsync(key.ToString(), default));
        Assert.Equal(native, (await reader.ResolveAsync(key, default))!.NativeId);

        // Completed provider snapshots are in provider-prefixed tables, outside common purchase rows.
        var providerDeadline = DateTimeOffset.FromUnixTimeMilliseconds(now.AddMinutes(15).ToUnixTimeMilliseconds()).ToOffset(TimeSpan.FromMinutes(210));
        var offsetSession = EntertainmentCheckoutSession.Create(Guid.NewGuid(), "touristpanel", "offset-session", "fingerprint", "request");
        db.CheckoutSessions.Add(offsetSession); await db.SaveChangesAsync();
        offsetSession.Reserved("external-cart", "protected-reservation", providerDeadline);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var storedSession = await db.CheckoutSessions.SingleAsync(x => x.Id == offsetSession.Id);
        Assert.Equal(providerDeadline, storedSession.PayableUntil!.Value);
        Assert.Equal(TimeSpan.Zero, storedSession.PayableUntil.Value.Offset);
        var purchase = EntertainmentPurchase.Create(Guid.NewGuid(), "test", "fingerprint", "name", "mobile",
            [new("touristpanel", supplier, key.ToString("N"), "offer", "option", "title", "offer", "entertainment", 1, 100, "{\"native\":true}")]);
        purchase.AttachSession(offsetSession.Id, providerDeadline, "external-cart", "protected-provider-context", "selection");
        db.Orders.Add(purchase); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var reloaded = await new EntertainmentRepository(db).GetOrderAsync(purchase.Id);
        Assert.Equal(purchase.Id, reloaded!.Id); Assert.Equal("protected-provider-context", reloaded.ReservationContextProtected);
        Assert.Equal(providerDeadline, reloaded.PayableUntil!.Value);
        Assert.Equal(TimeSpan.Zero, reloaded.PayableUntil.Value.Offset);
        Assert.True(System.Text.Json.JsonDocument.Parse(reloaded.Items.Single().ProviderPayloadJson).RootElement.GetProperty("native").GetBoolean());
        var issued = reloaded.Fulfillments.Single();
        var attempt = issued.BeginAttempt("fulfill", "postgres-stable-issue", "hash");
        await db.SaveChangesAsync(); attempt.Complete(); issued.Complete("invoice", [("hash-ticket", "encrypted-code", false)]);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Single((await new EntertainmentRepository(db).GetOrderAsync(purchase.Id))!.Fulfillments.Single().Tickets);
        await using var sql = new NpgsqlConnection(connection); await sql.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM information_schema.columns WHERE table_schema='entertainments' AND table_name IN ('purchases','purchase_items','checkout_sessions') AND column_name IN ('ProviderPayloadJson','ReservationContextProtected','ReservationReference','ReservationProtected')", sql);
        Assert.Equal(0L, await command.ExecuteScalarAsync());

        var calls = 0; var fail = false;
        var transport = new Transport(r => {
            if (r.Path.EndsWith("connect/token")) return Json("{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_in\":3600}");
            calls++;
            if (fail) return Json("<bad>");
            object value = r.Path.EndsWith("program/detail")
                ? new TpProgramDto { Id = native, SupplyChainHojreId = supplier, Title = "<b>ارکیده</b>", Type = TpProgramType.ProgramAsService,
                    IsActive = true, Description = "<script>bad()</script> شرح تازه", ProgramAddress = "پیروزی" }
                : new[] { new TpProgramListDto { Id = native, SupplyChainHojreId = supplier, IsActive = true, Type = TpProgramType.ProgramAsService,
                    Title = "ارکیده", LocationData = new() { Code = "tp-tehran", Native = "تهران" } } };
            return Json(System.Text.Json.JsonSerializer.Serialize(value));
        });
        db.ChangeTracker.Clear();
        var queued = await db.SyncStates.SingleAsync(); queued.Failed(now.AddMinutes(-16));
        await db.Database.ExecuteSqlRawAsync("UPDATE entertainments.touristpanel_programs SET \"NextDetailAttemptUtc\"=CURRENT_TIMESTAMP - interval '1 minute'");
        await db.SaveChangesAsync();
        var sync = new TouristPanelCatalog(db, Client(transport, settings), Options.Create(settings), Options.Create(new EntertainmentsOptions()), new FakeMedia(), NullLogger<TouristPanelCatalog>.Instance);
        await sync.RefreshAsync(default);
        Assert.True(calls >= 3); db.ChangeTracker.Clear();
        var updated = await db.Programs.SingleAsync(); Assert.Equal(key, updated.ProgramKey); Assert.Equal("شرح تازه", updated.Description);
        var successful = (await db.SyncStates.SingleAsync()).LastSuccessUtc;
        (await db.SyncStates.SingleAsync()).Failed(now.AddMinutes(-16)); await db.SaveChangesAsync(); fail = true;
        await Assert.ThrowsAnyAsync<Exception>(() => sync.RefreshAsync(default));
        Assert.Equal(successful, (await db.SyncStates.AsNoTracking().SingleAsync()).LastSuccessUtc);
        Assert.True((await db.Programs.AsNoTracking().SingleAsync()).IsListed);
        await db.Database.ExecuteSqlRawAsync("UPDATE entertainments.touristpanel_programs SET \"ObservedAtUtc\"=CURRENT_TIMESTAMP - interval '1 day'");
        Assert.True((await reader.SearchAsync(new(null, null, null), 20, default)).Items.Single().IsStale);
        await db.Database.ExecuteSqlRawAsync("UPDATE entertainments.touristpanel_programs SET \"ObservedAtUtc\"=CURRENT_TIMESTAMP - interval '8 days'");
        await Assert.ThrowsAsync<EntertainmentUnavailableException>(() => reader.SearchAsync(new(null, null, null), 20, default));
        await Assert.ThrowsAsync<EntertainmentUnavailableException>(() => reader.ProfileAsync(key, default));
        geography.Active = false; Assert.Empty(await reader.CitiesAsync(default));
        await db.Database.ExecuteSqlRawAsync("UPDATE entertainments.touristpanel_programs SET \"ObservedAtUtc\"=CURRENT_TIMESTAMP");
        Assert.Null(await reader.ProfileAsync(key, default));
        Assert.Null(await reader.ResolveNativeAsync(key.ToString(), default));

        // Exercise real EF tracking, orphan removal and the unique cart-selection index during upgrade.
        db.ChangeTracker.Clear();
        var cart = EntertainmentCart.Create(Guid.NewGuid());
        var selection = new EntertainmentCartItemSnapshot("touristpanel", "supplier", "program", "offer", "legacy", "title", "supplier", "program", null, "ticket", "offer", 1, 100, 100);
        cart.AddOrReplace(selection); db.Carts.Add(cart); await db.SaveChangesAsync();
        var originalId = cart.Items.Single().Id;
        cart.AddOrReplace(selection with { PurchaseOptionKey = "stable" }, ["legacy"]); await db.SaveChangesAsync();
        Assert.Equal(originalId, cart.Items.Single().Id);
        cart.AddOrReplace(selection with { PurchaseOptionKey = "legacy2" }); await db.SaveChangesAsync();
        cart.AddOrReplace(selection with { PurchaseOptionKey = "stable", Quantity = 2 }, ["legacy", "legacy2"]); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var persistedCart = await new EntertainmentRepository(db).GetCartAsync(cart.UserId);
        Assert.Equal(originalId, Assert.Single(persistedCart!.Items).Id);
        Assert.Equal("stable", persistedCart.Items[0].PurchaseOptionKey); Assert.Equal(2, persistedCart.Items[0].Quantity);

    }
    private sealed class Geography : ISender
    {
        public bool Active = true;
        public Task<T> Send<T>(IRequest<T> request, CancellationToken ct = default) => Task.FromResult((T)(object)(request switch {
            GetCitiesQuery => new GetCitiesResponse(Active ? [new(10,"تهران","Tehran","tehran",1,"تهران",0,true)] : []),
            GetProvincesQuery => new GetProvincesResponse([new(1,"تهران","Tehran","tehran",0,true)]),
            _ => throw new InvalidOperationException("Discovery attempted a non-geography request")
        }));
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<T> CreateStream<T>(IStreamRequest<T> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
