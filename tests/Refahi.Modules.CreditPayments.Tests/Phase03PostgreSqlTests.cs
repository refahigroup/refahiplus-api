using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase03PostgreSqlTests : IAsyncLifetime
{
    private const string Phase02Migration = "20260916102206_CreditPayments_AddMerchantTerminalCredentialFoundation";
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Migration_preserves_phase02_enforces_constraints_and_rolls_back_safely()
    {
        await using var db = await NewDatabaseAsync();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Phase02Migration);
        var merchant = ActiveMerchant("Snapshot Merchant");
        var terminal = merchant.AddTerminal("Snapshot Terminal", "store.clothing", 100, 1000,
            ["https://merchant.example/callback"], Now);
        db.Merchants.Add(merchant);
        await db.SaveChangesAsync();

        await db.Database.MigrateAsync();
        Assert.Equal(merchant.Id, (await db.Merchants.AsNoTracking().SingleAsync()).Id);
        var payment = await CreateAsync(db, merchant.Id, terminal.Id, "idem-migration-01", "ORDER-MIGRATION");
        Assert.Equal("awaiting_customer", payment.Status);
        await Assert.ThrowsAnyAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE credit_payments.gateway_payments SET currency = 'USD'"));

        db.ChangeTracker.Clear();
        await migrator.MigrateAsync(Phase02Migration);
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT to_regclass('credit_payments.gateway_payments')::text, (SELECT count(*) FROM credit_payments.merchants)", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(await reader.IsDBNullAsync(0));
        Assert.Equal(1L, reader.GetInt64(1));
    }

    [Fact]
    public async Task Sequential_idempotency_validation_snapshots_expiry_and_isolation_are_correct()
    {
        await using var db = await NewMigratedDatabaseAsync();
        var merchant = ActiveMerchant("Original Merchant");
        var terminalA = merchant.AddTerminal("Terminal A", "store.clothing", 100, 1000,
            ["https://MERCHANT.example:443/callback", "https://merchant.example/alternate"], Now);
        var terminalB = merchant.AddTerminal("Terminal B", "store", 100, 1000,
            ["https://merchant.example/other"], Now);
        db.Merchants.Add(merchant);
        var merchantB = ActiveMerchant("Merchant B");
        var terminalC = merchantB.AddTerminal("Terminal C", "hotel", 100, 1000,
            ["https://other.example/callback"], Now);
        db.Merchants.Add(merchantB);
        await db.SaveChangesAsync();

        var first = await CreateAsync(db, merchant.Id, terminalA.Id, "same-key-0001", "ORDER-1",
            metadata: new Dictionary<string, string> { ["z"] = "last", ["a"] = "first" });
        var retry = await CreateAsync(db, merchant.Id, terminalA.Id, "same-key-0001", "ORDER-1",
            now: Now.AddMinutes(1), metadata: new Dictionary<string, string> { ["a"] = "first", ["z"] = "last" });
        Assert.Equal(first.PaymentId, retry.PaymentId);
        Assert.Equal(first.CheckoutUrl, retry.CheckoutUrl);
        Assert.Equal(first.ExpiresAt, retry.ExpiresAt);
        Assert.Equal(Now.AddMinutes(15), first.ExpiresAt);
        Assert.Equal(1, await db.GatewayPayments.CountAsync());
        Assert.Equal(1, await db.IdempotencyRecords.CountAsync());
        Assert.Equal(1, await db.CheckoutSessions.CountAsync());
        Assert.Equal(GatewayPaymentStatus.AwaitingCustomer,
            (await db.GatewayPayments.AsNoTracking().SingleAsync()).Status);
        var rawToken = new Uri(first.CheckoutUrl).Segments[^1];
        var checkout = await db.CheckoutSessions.AsNoTracking().SingleAsync();
        Assert.DoesNotContain(rawToken, Convert.ToBase64String(checkout.TokenNonce));
        Assert.DoesNotContain(rawToken, Convert.ToBase64String(checkout.TokenHash));
        var audit = await db.AuditRecords.SingleAsync(x => x.Action == "gateway-payment.created");
        Assert.Equal(merchant.Id, audit.MerchantId);
        Assert.Equal(terminalA.Id, audit.TerminalId);
        Assert.Equal("ORDER-1", audit.MerchantOrderId);
        Assert.False(string.IsNullOrWhiteSpace(audit.ActorId));

        var amountConflict = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            CreateAsync(db, merchant.Id, terminalA.Id, "same-key-0001", "ORDER-1", amount: 101));
        Assert.Equal("idempotency_conflict", amountConflict.Code);
        var returnConflict = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            CreateAsync(db, merchant.Id, terminalA.Id, "same-key-0001", "ORDER-1", returnUrl: "https://merchant.example/alternate"));
        Assert.Equal("idempotency_conflict", returnConflict.Code);
        var expiryConflict = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            CreateAsync(db, merchant.Id, terminalA.Id, "same-key-0001", "ORDER-1", expires: 600));
        Assert.Equal("idempotency_conflict", expiryConflict.Code);
        var metadataConflict = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            CreateAsync(db, merchant.Id, terminalA.Id, "same-key-0001", "ORDER-1", metadata: new Dictionary<string, string> { ["a"] = "changed", ["z"] = "last" }));
        Assert.Equal("idempotency_conflict", metadataConflict.Code);
        var descriptionConflict = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            CreateAsync(db, merchant.Id, terminalA.Id, "same-key-0001", "ORDER-1", description: "changed"));
        Assert.Equal("idempotency_conflict", descriptionConflict.Code);
        var currencyConflict = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            CreateAsync(db, merchant.Id, terminalA.Id, "same-key-0001", "ORDER-1", currency: "USD"));
        Assert.Equal("idempotency_conflict", currencyConflict.Code);
        var orderConflict = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            CreateAsync(db, merchant.Id, terminalA.Id, "same-key-0001", "ORDER-CHANGED"));
        Assert.Equal("idempotency_conflict", orderConflict.Code);

        var duplicateOrder = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            CreateAsync(db, merchant.Id, terminalA.Id, "different-key-01", "ORDER-1"));
        Assert.Equal("merchant_order_already_exists", duplicateOrder.Code);

        var otherTerminal = await CreateAsync(db, merchant.Id, terminalB.Id, "same-key-0001", "ORDER-1",
            returnUrl: "https://merchant.example/other");
        Assert.NotEqual(first.PaymentId, otherTerminal.PaymentId);

        var repository = new CreditPaymentsRepository(db);
        var query = new GetGatewayPaymentHandler(repository);
        Assert.Null(await query.Handle(new(merchantB.Id, terminalC.Id, first.PaymentId), default));
        Assert.Null(await query.Handle(new(merchant.Id, terminalB.Id, first.PaymentId), default));
        Assert.NotNull(await query.Handle(new(merchant.Id, terminalA.Id, first.PaymentId), default));

        terminalA.UpdatePolicy("store.food", 100, 1000, Now.AddMinutes(2));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = await db.GatewayPayments.SingleAsync(x => x.Id == first.PaymentId);
        Assert.Equal("Original Merchant", stored.MerchantDisplayNameSnapshot);
        Assert.Equal("Terminal A", stored.TerminalNameSnapshot);
        Assert.Equal("store.clothing", stored.ApprovedCategoryCodeSnapshot);
        Assert.Equal("{\"a\": \"first\", \"z\": \"last\"}", stored.MetadataJson);
    }

    [Fact]
    public async Task Creation_rules_and_boundaries_are_enforced()
    {
        await using var seedDb = await NewMigratedDatabaseAsync();
        var merchant = ActiveMerchant("Merchant");
        var terminal = merchant.AddTerminal("Terminal", "store", 100, 1000,
            ["https://merchant.example/callback"], Now);
        seedDb.Merchants.Add(merchant);
        await seedDb.SaveChangesAsync();

        Assert.Equal(100, (await CreateAsync(seedDb, merchant.Id, terminal.Id, "boundary-min-01", "MIN", 100)).AmountMinor);
        Assert.Equal(1000, (await CreateAsync(seedDb, merchant.Id, terminal.Id, "boundary-max-01", "MAX", 1000)).AmountMinor);
        await AssertCode(seedDb, merchant.Id, terminal.Id, "below-minimum-1", "LOW", "amount_out_of_range", amount: 99);
        await AssertCode(seedDb, merchant.Id, terminal.Id, "above-maximum-1", "HIGH", "amount_out_of_range", amount: 1001);
        await AssertCode(seedDb, merchant.Id, terminal.Id, "currency-usd-001", "USD", "unsupported_currency", currency: "USD");
        await AssertCode(seedDb, merchant.Id, terminal.Id, "unknown-url-0001", "URL", "return_url_not_allowed", returnUrl: "https://merchant.example/not-registered");
        var otherMerchant = ActiveMerchant("Wrong owner");
        seedDb.Merchants.Add(otherMerchant);
        await seedDb.SaveChangesAsync();
        await AssertCode(seedDb, otherMerchant.Id, terminal.Id, "wrong-owner-0001", "OWNER", "terminal_not_active");
        await AssertCode(seedDb, merchant.Id, terminal.Id, "expiry-low-0001", "EXP1", "expiry_out_of_range", expires: 299);
        await AssertCode(seedDb, merchant.Id, terminal.Id, "expiry-high-0001", "EXP2", "expiry_out_of_range", expires: 1801);
        Assert.Equal(Now.AddMinutes(5), (await CreateAsync(seedDb, merchant.Id, terminal.Id, "expiry-min-0001", "EXP3", expires: 300)).ExpiresAt);
        Assert.Equal(Now.AddMinutes(30), (await CreateAsync(seedDb, merchant.Id, terminal.Id, "expiry-max-0001", "EXP4", expires: 1800)).ExpiresAt);
        var tooMany = Enumerable.Range(0, 21).ToDictionary(x => $"k{x}", x => "v");
        await AssertCode(seedDb, merchant.Id, terminal.Id, "metadata-many-01", "META", "invalid_metadata", metadata: tooMany);
        var tooLarge = Enumerable.Range(0, 20).ToDictionary(x => $"key-{x:D2}-" + new string('k', 30), x => new string('v', 200));
        await AssertCode(seedDb, merchant.Id, terminal.Id, "metadata-large-1", "META2", "invalid_metadata", metadata: tooLarge);
        await AssertCode(seedDb, merchant.Id, terminal.Id, "order-empty-0001", " ", "merchant_order_invalid");
        await AssertCode(seedDb, merchant.Id, terminal.Id, "order-long-00001", new string('x', 101), "merchant_order_invalid");

        merchant.Suspend(Now.AddMinutes(1));
        await seedDb.SaveChangesAsync();
        await AssertCode(seedDb, merchant.Id, terminal.Id, "merchant-off-001", "M-OFF", "merchant_not_active");
        merchant.Activate(Now.AddMinutes(2));
        merchant.Disable(Now.AddMinutes(3));
        await seedDb.SaveChangesAsync();
        await AssertCode(seedDb, merchant.Id, terminal.Id, "merchant-disabled", "M-DIS", "merchant_not_active");

        var pending = Merchant.Create(Guid.NewGuid(), "Pending", Now);
        seedDb.Merchants.Add(pending);
        await seedDb.SaveChangesAsync();
        await AssertCode(seedDb, pending.Id, Guid.NewGuid(), "merchant-pending1", "M-PEN", "merchant_not_active");

        var merchant2 = ActiveMerchant("Merchant 2");
        var terminal2 = merchant2.AddTerminal("Terminal 2", "store", 100, 1000,
            ["https://merchant.example/callback"], Now);
        seedDb.Merchants.Add(merchant2);
        await seedDb.SaveChangesAsync();
        terminal2.Suspend(Now.AddMinutes(1));
        await seedDb.SaveChangesAsync();
        await AssertCode(seedDb, merchant2.Id, terminal2.Id, "terminal-suspend", "T-SUS", "terminal_not_active");
        terminal2.Activate(Now.AddMinutes(2));
        terminal2.Disable(Now.AddMinutes(3));
        await seedDb.SaveChangesAsync();
        await AssertCode(seedDb, merchant2.Id, terminal2.Id, "terminal-disabled", "T-DIS", "terminal_not_active");
    }

    [Fact]
    public async Task Concurrent_same_key_same_request_creates_one_payment_and_returns_same_result()
    {
        await using var seedDb = await NewMigratedDatabaseAsync();
        var merchant = ActiveMerchant("Merchant");
        var terminal = merchant.AddTerminal("Terminal", "store", 100, 1000,
            ["https://merchant.example/callback"], Now);
        seedDb.Merchants.Add(merchant);
        await seedDb.SaveChangesAsync();
        var connectionString = seedDb.Database.GetConnectionString()!;

        async Task<CreateGatewayPaymentResponse> Run()
        {
            await using var db = Create(connectionString);
            return await CreateAsync(db, merchant.Id, terminal.Id, "concurrent-key-1", "CONCURRENT-1");
        }
        var results = await Task.WhenAll(Task.Run(Run), Task.Run(Run));
        Assert.Equal(results[0].PaymentId, results[1].PaymentId);
        Assert.Equal(results[0].CheckoutUrl, results[1].CheckoutUrl);
        await using var verify = Create(connectionString);
        Assert.Equal(1, await verify.GatewayPayments.CountAsync());
        Assert.Equal(1, await verify.IdempotencyRecords.CountAsync());
        Assert.Equal(1, await verify.CheckoutSessions.CountAsync());
        Assert.Equal(1, await verify.AuditRecords.CountAsync(x => x.Action == "gateway-payment.created"));
    }

    [Fact]
    public async Task Concurrent_different_keys_same_order_has_one_winner_and_deterministic_conflict()
    {
        await using var seedDb = await NewMigratedDatabaseAsync();
        var merchant = ActiveMerchant("Merchant");
        var terminal = merchant.AddTerminal("Terminal", "store", 100, 1000,
            ["https://merchant.example/callback"], Now);
        seedDb.Merchants.Add(merchant);
        await seedDb.SaveChangesAsync();
        var connectionString = seedDb.Database.GetConnectionString()!;

        async Task<object> Run(string key)
        {
            await using var db = Create(connectionString);
            try { return await CreateAsync(db, merchant.Id, terminal.Id, key, "SAME-ORDER"); }
            catch (CreditPaymentsApplicationException ex) { return ex; }
        }
        var results = await Task.WhenAll(Task.Run(() => Run("different-key-a")), Task.Run(() => Run("different-key-b")));
        Assert.Single(results.OfType<CreateGatewayPaymentResponse>());
        var conflict = Assert.Single(results.OfType<CreditPaymentsApplicationException>());
        Assert.Equal("merchant_order_already_exists", conflict.Code);
        await using var verify = Create(connectionString);
        Assert.Equal(1, await verify.GatewayPayments.CountAsync());
        Assert.Equal(1, await verify.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task Gateway_payment_version_rejects_stale_updates()
    {
        await using var seedDb = await NewMigratedDatabaseAsync();
        var merchant = ActiveMerchant("Merchant");
        var terminal = merchant.AddTerminal("Terminal", "store", 100, 1000,
            ["https://merchant.example/callback"], Now);
        seedDb.Merchants.Add(merchant);
        await seedDb.SaveChangesAsync();
        var dto = await CreateAsync(seedDb, merchant.Id, terminal.Id, "version-key-0001", "VERSION");
        var cs = seedDb.Database.GetConnectionString()!;
        await using var a = Create(cs);
        await using var b = Create(cs);
        var pa = await a.GatewayPayments.SingleAsync(x => x.Id == dto.PaymentId);
        var pb = await b.GatewayPayments.SingleAsync(x => x.Id == dto.PaymentId);
        a.Entry(pa).Property(x => x.Version).CurrentValue++;
        await a.SaveChangesAsync();
        b.Entry(pb).Property(x => x.Version).CurrentValue++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    private async Task<CreditPaymentsDbContext> NewMigratedDatabaseAsync()
    {
        var db = await NewDatabaseAsync();
        await db.Database.MigrateAsync();
        return db;
    }

    private async Task<CreditPaymentsDbContext> NewDatabaseAsync()
    {
        var database = "cpg_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        var builder = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString()) { Database = database };
        return Create(builder.ConnectionString);
    }

    private static CreditPaymentsDbContext Create(string connectionString) => new(
        new DbContextOptionsBuilder<CreditPaymentsDbContext>().UseNpgsql(connectionString).Options);

    private static Merchant ActiveMerchant(string name)
    {
        var merchant = Merchant.Create(Guid.NewGuid(), name, Now);
        merchant.Activate(Now);
        return merchant;
    }

    private static async Task<CreateGatewayPaymentResponse> CreateAsync(CreditPaymentsDbContext db, Guid merchantId, Guid terminalId,
        string key, string orderId, long amount = 100, string currency = "IRR",
        string returnUrl = "https://merchant.example/callback", int? expires = null,
        DateTimeOffset? now = null, IReadOnlyDictionary<string, string>? metadata = null, string? description = "description")
    {
        var repository = new CreditPaymentsRepository(db);
        var checkoutOptions = Options.Create(new CheckoutOptions
        {
            PublicBaseUrl = "https://pay.example",
            TokenKeys = new() { [1] = Convert.ToBase64String(new byte[32]) }
        });
        var security = new HmacCheckoutSecurityService(checkoutOptions);
        var handler = new CreateGatewayPaymentHandler(repository, repository, repository, security,
            Options.Create(new GatewayPaymentOptions()), checkoutOptions,
            new FixedTimeProvider(now ?? Now));
        return await handler.Handle(new(merchantId, terminalId, Guid.NewGuid(), key, orderId, amount, currency,
            description, returnUrl, expires, metadata), default);
    }

    private static async Task AssertCode(CreditPaymentsDbContext db, Guid merchantId, Guid terminalId, string key,
        string orderId, string code, long amount = 100, string currency = "IRR",
        string returnUrl = "https://merchant.example/callback", int? expires = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var exception = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            CreateAsync(db, merchantId, terminalId, key, orderId, amount, currency, returnUrl, expires, metadata: metadata));
        Assert.Equal(code, exception.Code);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
