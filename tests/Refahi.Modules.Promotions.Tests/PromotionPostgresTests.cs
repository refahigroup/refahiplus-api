using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Exceptions;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;
using Refahi.Modules.Organizations.Application.Contracts;
using Refahi.Modules.Organizations.Infrastructure;
using Refahi.Modules.Promotions.Application.Contracts;
using Refahi.Modules.Promotions.Infrastructure;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Context;
using Xunit;

namespace Refahi.Modules.Promotions.Tests;

[CollectionDefinition("Promotions database", DisableParallelization = true)]
public sealed class PromotionDatabaseCollection : ICollectionFixture<PromotionDatabase>;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REFAHI_PROMOTIONS_TEST_CONNECTION"))) Skip = "REFAHI_PROMOTIONS_TEST_CONNECTION is not configured"; }
}
public sealed class PostgresTheoryAttribute : TheoryAttribute
{ public PostgresTheoryAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REFAHI_PROMOTIONS_TEST_CONNECTION"))) Skip = "REFAHI_PROMOTIONS_TEST_CONNECTION is not configured"; } }
public sealed class AdjustableClock : TimeProvider
{
    private long _offset;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddTicks(Interlocked.Read(ref _offset));
    public void Advance(TimeSpan amount) => Interlocked.Add(ref _offset, amount.Ticks);
    public void Reset() => Interlocked.Exchange(ref _offset, 0);
}

public sealed class PromotionDatabase : IAsyncLifetime
{
    public string Connection => Environment.GetEnvironmentVariable("REFAHI_PROMOTIONS_TEST_CONNECTION")!;
    public Guid FundingWallet { get; } = Guid.NewGuid();
    public AdjustableClock Clock { get; } = new();
    public ServiceProvider Services { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var parsed = new NpgsqlConnectionStringBuilder(Connection);
        Assert.Contains("_test", parsed.Database!, StringComparison.OrdinalIgnoreCase);
        await using (var db = new PromotionDbContext(new DbContextOptionsBuilder<PromotionDbContext>().UseNpgsql(Connection).Options)) await db.Database.MigrateAsync();
        await using (var db = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(Connection).Options)) await db.Database.MigrateAsync();
        // An existing Orders migration backfills ownership from the Store schema.
        await using (var connection = new NpgsqlConnection(Connection))
        {
            if (await connection.ExecuteScalarAsync<string?>("select to_regclass('store.shops')::text") == null)
            {
                await using var store = new Refahi.Modules.Store.Infrastructure.Persistence.Context.StoreDbContext(new DbContextOptionsBuilder<Refahi.Modules.Store.Infrastructure.Persistence.Context.StoreDbContext>().UseNpgsql(Connection).Options);
                await connection.ExecuteAsync(store.Database.GenerateCreateScript());
            }
        }
        await using (var db = new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(Connection).Options)) await db.Database.MigrateAsync();
        await using (var db = new WalletsDbContext(new DbContextOptionsBuilder<WalletsDbContext>().UseNpgsql(Connection).Options)) await db.Database.MigrateAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStringName"] = "Test",
            ["ConnectionStrings:Test"] = Connection,
            ["Promotions:Enabled"] = "true",
            ["Promotions:FundingWalletId"] = FundingWallet.ToString()
        }).Build();
        var services = new ServiceCollection(); services.AddSingleton<IConfiguration>(config); services.AddLogging(); services.AddDataProtection().UseEphemeralDataProtectionProvider();
        Refahi.Modules.Promotions.Api.DI.RegisterPromotionsModule(services, config);
        Refahi.Modules.Organizations.Api.DI.RegisterOrganizationsModule(services, config);
        Refahi.Modules.Orders.Api.DI.RegisterOrdersModule(services, config);
        Refahi.Modules.Wallets.Api.DI.RegisterWalletsModule(services, config);
        services.AddTransient<IRequestHandler<GetOrderUserSummariesQuery, IReadOnlyList<OrderUserSummaryDto>>, TestIdentityHandler>();
        services.AddSingleton<TimeProvider>(Clock);
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await SeedWallet(FundingWallet, Guid.NewGuid(), 1, 10_000_000);
    }
    public async Task SeedWallet(Guid id, Guid owner, int type, long balance)
    {
        await using var c = new NpgsqlConnection(Connection);
        await c.ExecuteAsync("""
            insert into wallets.wallets (wallet_id,"OwnerId",wallet_type,status,currency,created_at)
            values (@Id,@Owner,@Type,1,'IRR',now());
            insert into wallets.wallet_balances(wallet_id,available_minor,pending_minor,currency,version,updated_at)
            values (@Id,@Balance,0,'IRR',1,now());
            """, new { Id = id, Owner = owner, Type = (short)type, Balance = balance });
    }
    public async Task<(long Available, long Pending)> Balance(Guid id)
    { await using var c = new NpgsqlConnection(Connection); return await c.QuerySingleAsync<(long, long)>("select available_minor,pending_minor from wallets.wallet_balances where wallet_id=@Id", new { Id = id }); }
    public async Task DisposeAsync() { if (Services != null) await Services.DisposeAsync(); }
}
public sealed class TestIdentityHandler : IRequestHandler<GetOrderUserSummariesQuery, IReadOnlyList<OrderUserSummaryDto>>
{
    public Task<IReadOnlyList<OrderUserSummaryDto>> Handle(GetOrderUserSummariesQuery q, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OrderUserSummaryDto>>(q.UserIds!.Select(x => new OrderUserSummaryDto(x, "تست", "کاربر", null)).ToList());
}

[Collection("Promotions database")]
public sealed class PromotionPostgresTests(PromotionDatabase database)
{
    private async Task<(Guid Campaign, string Code)> Campaign(long budget = 100000, int uses = 100, Guid[]? organizations = null)
    {
        using var scope = database.Services.CreateScope(); var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var rules = new CampaignRules("تست", "Fixed", 100, 0, 100, 0, budget, uses, 1, [], [], organizations ?? [], [], DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var c = await sender.Send(new SaveCampaignCommand(null, rules, Guid.NewGuid()));
        var code = "T" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        await sender.Send(new GenerateCodesCommand(c.Id, code, null, 1, 100, Guid.NewGuid().ToString("N"), Guid.NewGuid()));
        await sender.Send(new SetCampaignActiveCommand(c.Id, true, Guid.NewGuid())); return (c.Id, code);
    }
    private async Task<(Guid Id, Guid User, Guid Wallet)> Order(string module = "Store",long balance = 10000)
    {
        var user = Guid.NewGuid(); var wallet = Guid.NewGuid(); await database.SeedWallet(wallet, user, 2, balance);
        using var scope = database.Services.CreateScope(); var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var o = Refahi.Modules.Orders.Domain.Aggregates.Order.Create(user, module, Guid.NewGuid(), Guid.NewGuid().ToString("N"), null,
            [new OrderItemData("آیتم", 1000, 1, 0, Guid.NewGuid(), "store.clothing", null, null)], payableUntil: DateTimeOffset.UtcNow.AddMinutes(30));
        await repo.AddAsync(o); return (o.Id, user, wallet);
    }
    private async Task<T> Send<T>(IRequest<T> r) { using var scope = database.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<ISender>().Send(r); }
    [PostgresFact]
    public async Task DiscountedOrder_PaysBothSources_RefundsOriginalWallets_AndRestoresQuota()
    {
        var campaign = await Campaign(); var o = await Order(); var originalFunding = await database.Balance(database.FundingWallet);
        var applied = await Send(new ApplyOrderPromotionCommand(o.Id, o.User, campaign.Code, 1, "apply"));
        Assert.Equal(900, applied.FinalAmountMinor); Assert.Equal(2, applied.PricingVersion);
        var replay = await Send(new ApplyOrderPromotionCommand(o.Id, o.User, campaign.Code, 1, "apply")); Assert.Equal(applied.ReservationId, replay.ReservationId);
        var paid = await Send(new PayOrderCommand(o.Id, o.User, "User", [new(o.Wallet, 900)], "pay", ExpectedPricingVersion: 2));
        Assert.Equal(9100, (await database.Balance(o.Wallet)).Available); Assert.Equal(originalFunding.Available - 100, (await database.Balance(database.FundingWallet)).Available);
        await Send(new RecoverOrderPromotionCommand(o.Id));
        Assert.Equal("Committed", (await Send(new GetPromotionReservationQuery(applied.ReservationId!.Value)))!.Status);
        await Send(new CancelOrderCommand(o.Id, "تست", "cancel", CallerRole: "System")); await Send(new RecoverOrderPromotionCommand(o.Id));
        Assert.Equal(10000, (await database.Balance(o.Wallet)).Available); Assert.Equal(originalFunding, await database.Balance(database.FundingWallet));
        Assert.Equal("Reversed", (await Send(new GetPromotionReservationQuery(applied.ReservationId.Value)))!.Status);
        Assert.Equal(0, (await Send(new GetCampaignQuery(campaign.Campaign))).OccupiedUses);
        // A duplicate/late payment notification cannot resurrect a refunded redemption.
        using var scope = database.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new OrderPaidIntegrationEvent(o.Id, "test", o.User, "Store", null, "Store", null, paid.PaymentId, 900, DateTimeOffset.UtcNow));
        Assert.Equal("Reversed", (await Send(new GetPromotionReservationQuery(applied.ReservationId.Value)))!.Status);
    }
    [PostgresFact]
    public async Task ConcurrentOrders_CannotExceedLastBudgetSlot()
    {
        var campaign = await Campaign(100, 1); var a = await Order(); var b = await Order();
        async Task<bool> Apply((Guid Id, Guid User, Guid Wallet) o)
        { try { await Send(new ApplyOrderPromotionCommand(o.Id, o.User, campaign.Code, 1, "apply")); return true; } catch (PromotionException) { return false; } }
        var result = await Task.WhenAll(Apply(a), Apply(b)); Assert.Single(result,x=>x);
        var usage = await Send(new GetCampaignQuery(campaign.Campaign)); Assert.Equal(100, usage.OccupiedBudgetMinor); Assert.Equal(1, usage.OccupiedUses);
    }
    [PostgresFact]
    public async Task RevokedMembership_PreventsPayment()
    {
        var o = await Order(); var org = await Send(new SaveOrganizationCommand(null, Guid.NewGuid().ToString("N"), "سازمان"));
        var member = await Send(new AddOrganizationMemberCommand(org.Id, o.User, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1), null));
        var campaign = await Campaign(organizations: [org.Id]); var applied = await Send(new ApplyOrderPromotionCommand(o.Id, o.User, campaign.Code, 1, "apply"));
        await Send(new RevokeOrganizationMemberCommand(org.Id, member.Id, Guid.NewGuid()));
        await Assert.ThrowsAsync<PromotionException>(() => Send(new PayOrderCommand(o.Id, o.User, "User", [new(o.Wallet, 900)], "pay", ExpectedPricingVersion: applied.PricingVersion)));
        Assert.Equal((10000L, 0L), await database.Balance(o.Wallet));
    }
    [PostgresFact]
    public async Task StaleVersion_AndForeignWallet_AreRejected()
    {
        var campaign = await Campaign(); var o = await Order(); var foreign = await Order(); await Send(new ApplyOrderPromotionCommand(o.Id, o.User, campaign.Code, 1, "apply"));
        await Assert.ThrowsAsync<OrderStateConflictException>(() => Send(new PayOrderCommand(o.Id, o.User, "User", [new(o.Wallet, 900)], "pay", ExpectedPricingVersion: 1)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Send(new PayOrderCommand(o.Id, o.User, "User", [new(foreign.Wallet, 900)], "pay", ExpectedPricingVersion: 2)));
    }
    [PostgresFact]
    public async Task CaptureBeforeOrderSave_IsRecoveredWithoutSecondDebit()
    {
        var campaign = await Campaign(); var o = await Order(); var applied = await Send(new ApplyOrderPromotionCommand(o.Id, o.User, campaign.Code, 1, "apply"));
        await Send(new BeginPromotionPaymentCommand(applied.ReservationId!.Value, o.Id, o.User));
        var writer = new PaymentAtomicWriter(database.Connection);
        var reserve = await writer.ExecuteCreateIntentAsync(o.Id, $"order-reserve-{o.Id:N}", 900, "IRR", [new(o.Wallet, 900), new(database.FundingWallet, 100, true)], null, null, default);
        var capture = await writer.ExecuteCaptureIntentAsync(reserve.IntentId, $"order-capture-{o.Id:N}", default);
        await Send(new RecoverOrderPromotionCommand(o.Id));
        var replay = await Send(new PayOrderCommand(o.Id, o.User, "User", [new(o.Wallet, 900)], "retry", ExpectedPricingVersion: 2));
        Assert.Equal(capture.PaymentId, replay.PaymentId); Assert.Equal(9100, (await database.Balance(o.Wallet)).Available);
    }
    [PostgresFact]
    public async Task AdditionalFunding_InsufficientBalance_RollsBackCustomerHold()
    {
        var customer = Guid.NewGuid(); var sponsor = Guid.NewGuid(); await database.SeedWallet(customer, Guid.NewGuid(), 2, 1000); await database.SeedWallet(sponsor, Guid.NewGuid(), 1, 50);
        var writer = new PaymentAtomicWriter(database.Connection);
        await Assert.ThrowsAnyAsync<Exception>(() => writer.ExecuteCreateIntentAsync(Guid.NewGuid(), "key", 900, "IRR", [new(customer, 900), new(sponsor, 100, true)], null, null, default));
        Assert.Equal((1000L, 0L), await database.Balance(customer)); Assert.Equal((50L, 0L), await database.Balance(sponsor));
    }
    [PostgresFact]
    public async Task AdditionalFunding_DestinationAndRefundUseCompleteServiceAmount()
    {
        var customer = Guid.NewGuid(); var sponsor = Guid.NewGuid(); var provider = Guid.NewGuid();
        await database.SeedWallet(customer, Guid.NewGuid(), 2, 1000); await database.SeedWallet(sponsor, Guid.NewGuid(), 1, 1000); await database.SeedWallet(provider, Guid.NewGuid(), 3, 0);
        var writer = new PaymentAtomicWriter(database.Connection); var intent = await writer.ExecuteCreateIntentAsync(Guid.NewGuid(), "key", 900, "IRR", [new(customer, 900), new(sponsor, 100, true)], null, provider, default);
        using(var scope=database.Services.CreateScope())
        {
            var read=await scope.ServiceProvider.GetRequiredService<Refahi.Modules.Wallets.Application.Contracts.Repositories.IPaymentReadRepository>().GetPaymentIntentAsync(intent.IntentId);
            Assert.Equal(customer,Assert.Single(read!.Allocations).WalletId);Assert.Equal(900,read.AmountMinor);Assert.Equal(TimeSpan.Zero,read.CreatedAt.Offset);
        }
        var paid = await writer.ExecuteCaptureIntentAsync(intent.IntentId, "capture", default); Assert.Equal(1000, (await database.Balance(provider)).Available);
        using(var scope=database.Services.CreateScope())
        {
            var read=await scope.ServiceProvider.GetRequiredService<Refahi.Modules.Wallets.Application.Contracts.Repositories.IPaymentReadRepository>().GetPaymentAsync(paid.PaymentId);
            Assert.Equal(customer,Assert.Single(read!.Allocations).WalletId);Assert.Equal(900,read.AmountMinor);
        }
        var refund = await writer.ExecuteRefundPaymentAsync(paid.PaymentId, "refund", null, null, default);
        using (var scope = database.Services.CreateScope())
        {
            var read = await scope.ServiceProvider.GetRequiredService<Refahi.Modules.Wallets.Application.Contracts.Repositories.IPaymentReadRepository>().GetRefundAsync(paid.PaymentId, refund.RefundId);
            Assert.Equal(customer, Assert.Single(read!.Allocations).WalletId);
        }
        Assert.Equal((1000L, 0L), await database.Balance(customer)); Assert.Equal((1000L, 0L), await database.Balance(sponsor)); Assert.Equal((0L, 0L), await database.Balance(provider));
    }
    [PostgresTheory]
    [InlineData("Store")]
    [InlineData("Hotel")]
    [InlineData("Flight")]
    [InlineData("Charge")]
    [InlineData("Commerce")]
    [InlineData("Cinema")]
    public async Task CentralOrders_ApplyAndPay_WithoutPromotionReferencesInSource(string module)
    {
        var campaign = await Campaign(); var o = await Order(module);
        var applied = await Send(new ApplyOrderPromotionCommand(o.Id, o.User, campaign.Code, 1, "apply"));
        var paid = await Send(new PayOrderCommand(o.Id, o.User, "User", [new(o.Wallet, 900)], "pay", ExpectedPricingVersion: applied.PricingVersion));
        Assert.Equal("Paid", paid.Status); Assert.Equal(9100, (await database.Balance(o.Wallet)).Available);
    }
    [PostgresFact]
    public async Task ExpiredReservation_RestoresPrice_AndRequiresNewConfirmation()
    {
        try
        {
            var campaign = await Campaign(); var o = await Order(); await Send(new ApplyOrderPromotionCommand(o.Id, o.User, campaign.Code, 1, "apply"));
            database.Clock.Advance(TimeSpan.FromMinutes(16)); await Send(new RecoverOrderPromotionCommand(o.Id));
            using var scope = database.Services.CreateScope(); var order = await scope.ServiceProvider.GetRequiredService<IOrderRepository>().GetByIdWithItemsAsync(o.Id);
            Assert.Equal(1000, order!.FinalAmountMinor); Assert.Equal(3, order.PricingVersion); Assert.Null(order.PromotionReservationId);
            await Assert.ThrowsAsync<OrderStateConflictException>(() => Send(new PayOrderCommand(o.Id, o.User, "User", [new(o.Wallet, 1000)], "pay", ExpectedPricingVersion: 2)));
            Assert.Equal(0, (await Send(new GetCampaignQuery(campaign.Campaign))).OccupiedUses);
        }
        finally { database.Clock.Reset(); }
    }
    [PostgresFact]
    public async Task RemoveAndSwitchCode_ReleasesCapacity_AndRejectsOldPaymentVersion()
    {
        var a = await Campaign(100, 1); var b = await Campaign(); var o = await Order(); await Send(new ApplyOrderPromotionCommand(o.Id, o.User, a.Code, 1, "apply-a"));
        await Assert.ThrowsAsync<OrderStateConflictException>(() => Send(new ApplyOrderPromotionCommand(o.Id, o.User, b.Code, 2, "apply-b")));
        var removed = await Send(new RemoveOrderPromotionCommand(o.Id, o.User, 2, "remove")); Assert.Equal(1000, removed.FinalAmountMinor);
        Assert.Equal(0, (await Send(new GetCampaignQuery(a.Campaign))).OccupiedUses);
        var applied = await Send(new ApplyOrderPromotionCommand(o.Id, o.User, b.Code, 3, "apply-b")); Assert.Equal(4, applied.PricingVersion);
        await Assert.ThrowsAsync<OrderStateConflictException>(() => Send(new RemoveOrderPromotionCommand(o.Id, o.User, 3, "remove")));
    }
    [PostgresFact]
    public async Task PrivateCodes_AreMaskedEncryptedAndBoundToTheirOwner()
    {
        var campaign = await Campaign(); var owner = Guid.NewGuid(); var actor = Guid.NewGuid();
        var codes = await Send(new GenerateCodesCommand(campaign.Campaign, null, owner, 3, 1, "batch", actor));
        var replay = await Send(new GenerateCodesCommand(campaign.Campaign, null, owner, 3, 1, "batch", actor)); Assert.Equal(codes.Select(x => x.Id), replay.Select(x => x.Id));
        var exported = await Send(new ExportCodesCommand(campaign.Campaign, codes[0].BatchKey, actor)); Assert.Equal(3, exported.Count);
        Assert.All(exported, x => Assert.Equal(16, x.Code.Length));
        await using var db = new PromotionDbContext(new DbContextOptionsBuilder<PromotionDbContext>().UseNpgsql(database.Connection).Options);
        var stored = await db.Codes.SingleAsync(x => x.Id == codes[0].Id);
        Assert.DoesNotContain(exported.Single(x => x.Id == stored.Id).Code, stored.Ciphertext); Assert.Equal(64, stored.Hash.Length);
        var o = await Order(); var context = new PromotionContext(o.Id, o.User, "Store", 1000, [new(Guid.NewGuid(), "store", 1000)], null);
        await Assert.ThrowsAsync<PromotionException>(() => Send(new PreviewPromotionQuery(exported[0].Code, context)));
        await Assert.ThrowsAsync<PromotionException>(() => Send(new GenerateCodesCommand(campaign.Campaign, null, owner, 2, 1, "batch", actor)));
    }
    [PostgresFact]
    public async Task AdditionalFunding_ReleaseRestoresBothHolds_AndPayloadReplayIsChecked()
    {
        var customer = Guid.NewGuid(); var sponsor = Guid.NewGuid(); await database.SeedWallet(customer, Guid.NewGuid(), 2, 1000); await database.SeedWallet(sponsor, Guid.NewGuid(), 1, 1000);
        var writer = new PaymentAtomicWriter(database.Connection); var order = Guid.NewGuid();
        var intent = await writer.ExecuteCreateIntentAsync(order, "key", 900, "IRR", [new(customer, 900), new(sponsor, 100, true)], null, null, default);
        Assert.Equal((100L, 900L), await database.Balance(customer)); Assert.Equal((900L, 100L), await database.Balance(sponsor));
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.ExecuteCreateIntentAsync(order, "key", 800, "IRR", [new(customer, 800), new(sponsor, 200, true)], null, null, default));
        await writer.ExecuteReleaseIntentAsync(intent.IntentId, "release", default); await writer.ExecuteReleaseIntentAsync(intent.IntentId, "release", default);
        Assert.Equal((1000L, 0L), await database.Balance(customer)); Assert.Equal((1000L, 0L), await database.Balance(sponsor));
    }
    [PostgresFact]
    public async Task AdditionalFunding_KeepsGrossInPersonPostingsBalanced()
    {
        var customer = Guid.NewGuid(); var sponsor = Guid.NewGuid(); var provider = Guid.NewGuid(); var revenue = Guid.NewGuid();
        await database.SeedWallet(customer, Guid.NewGuid(), 2, 1000); await database.SeedWallet(sponsor, Guid.NewGuid(), 1, 1000);
        await database.SeedWallet(provider, Guid.NewGuid(), 3, 0); await database.SeedWallet(revenue, Guid.NewGuid(), 1, 0);
        var writer = new PaymentAtomicWriter(database.Connection);
        var intent = await writer.ExecuteCreateIntentAsync(Guid.NewGuid(), "key", 900, "IRR", [new(customer, 900), new(sponsor, 100, true)], null, null, default,
            [new(provider, 1, 1000, "vendor-gross"), new(provider, 2, 100, "commission"), new(revenue, 1, 100, "platform-revenue")]);
        var payment = await writer.ExecuteCaptureIntentAsync(intent.IntentId, "capture", default);
        Assert.Equal(900, (await database.Balance(provider)).Available); Assert.Equal(100, (await database.Balance(revenue)).Available);
        await writer.ExecuteRefundPaymentAsync(payment.PaymentId, "refund", null, null, default);
        Assert.Equal((1000L, 0L), await database.Balance(customer)); Assert.Equal((1000L, 0L), await database.Balance(sponsor));
        Assert.Equal((0L, 0L), await database.Balance(provider)); Assert.Equal((0L, 0L), await database.Balance(revenue));
    }
    [PostgresFact]
    public async Task RefundEvents_AreDurableAndOutboxDeliveryRestoresQuota()
    {
        var campaign = await Campaign(); var o = await Order("Charge"); await Send(new ApplyOrderPromotionCommand(o.Id, o.User, campaign.Code, 1, "apply"));
        await Send(new PayOrderCommand(o.Id, o.User, "User", [new(o.Wallet, 900)], "pay", ExpectedPricingVersion: 2));
        await Send(new CancelOrderCommand(o.Id, "test", "cancel", CallerRole: "System"));
        await using (var db = new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(database.Connection).Options))
        {
            var messages = await db.OutboxMessages.Where(x => x.EventData.Contains(o.Id.ToString())).ToListAsync();
            Assert.Contains(messages, x => x.EventType.Contains(nameof(OrderRefundedIntegrationEvent)));
            foreach (var message in messages) message.OccurredAt = DateTimeOffset.UtcNow.AddYears(-1);
            await db.SaveChangesAsync();
        }
        var worker = database.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<Refahi.Modules.Orders.Infrastructure.Outbox.ProcessOutboxMessagesJob>().Single();
        await worker.StartAsync(default);
        try
        {
            var complete = false;
            for (var i = 0; i < 40; i++)
            {
                if ((await Send(new GetCampaignQuery(campaign.Campaign))).OccupiedUses == 0) { complete = true; break; }
                await Task.Delay(500);
            }
            Assert.True(complete, "Durable refund delivery must restore the campaign quota.");
        }
        finally { await worker.StopAsync(default); }
    }
    [PostgresFact]
    public async Task UnknownLocallyReservedIntent_PreventsRemovingTheDiscount()
    {
        var campaign = await Campaign(); var o = await Order("Charge"); var applied = await Send(new ApplyOrderPromotionCommand(o.Id, o.User, campaign.Code, 1, "apply"));
        await Send(new BeginPromotionPaymentCommand(applied.ReservationId!.Value, o.Id, o.User));
        var writer = new PaymentAtomicWriter(database.Connection);
        await writer.ExecuteCreateIntentAsync(o.Id, $"order-reserve-{o.Id:N}", 900, "IRR", [new(o.Wallet, 900), new(database.FundingWallet, 100, true)], null, null, default);
        await Assert.ThrowsAsync<OrderStateConflictException>(() => Send(new RemoveOrderPromotionCommand(o.Id, o.User, 2, "remove")));
        Assert.Equal(900, (await database.Balance(o.Wallet)).Pending);
        await Send(new CancelOrderCommand(o.Id, "test", "cancel", CallerRole: "System")); await Send(new RecoverOrderPromotionCommand(o.Id));
        Assert.Equal((10000L, 0L), await database.Balance(o.Wallet)); Assert.Equal(0, (await Send(new GetCampaignQuery(campaign.Campaign))).OccupiedUses);
    }
    [PostgresFact]
    public async Task PaymentPendingWithoutAnIntent_RechecksRevokedMembership()
    {
        var o = await Order(); var org = await Send(new SaveOrganizationCommand(null, Guid.NewGuid().ToString("N"), "سازمان"));
        var member = await Send(new AddOrganizationMemberCommand(org.Id, o.User, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1), null));
        var campaign = await Campaign(organizations: [org.Id]); var applied = await Send(new ApplyOrderPromotionCommand(o.Id, o.User, campaign.Code, 1, "apply"));
        await Send(new BeginPromotionPaymentCommand(applied.ReservationId!.Value, o.Id, o.User));
        await Send(new RevokeOrganizationMemberCommand(org.Id, member.Id, Guid.NewGuid()));
        await Assert.ThrowsAsync<PromotionException>(() => Send(new PayOrderCommand(o.Id, o.User, "User", [new(o.Wallet, 900)], "retry", ExpectedPricingVersion: 2)));
        Assert.Equal((10000L, 0L), await database.Balance(o.Wallet));
    }
    [PostgresFact]
    public async Task InvalidCode_ReplaysFailure_WithoutBlockingUnpromotedPayment()
    {
        var o = await Order(); var first = await Assert.ThrowsAsync<PromotionException>(() => Send(new ApplyOrderPromotionCommand(o.Id, o.User, "ABSENT", 1, "apply")));
        var replay = await Assert.ThrowsAsync<PromotionException>(() => Send(new ApplyOrderPromotionCommand(o.Id, o.User, "ABSENT", 1, "apply"))); Assert.Equal(first.Message, replay.Message);
        var paid = await Send(new PayOrderCommand(o.Id, o.User, "User", [new(o.Wallet, 1000)], "pay")); Assert.Equal("Paid", paid.Status);
    }
    [PostgresFact]
    public async Task ReservedPayment_UsesOriginalHeldWallets_WithoutDemandingAnotherTopUp()
    {
        var campaign=await Campaign();var o=await Order("Charge",900);
        var applied=await Send(new ApplyOrderPromotionCommand(o.Id,o.User,campaign.Code,1,"apply"));
        await Send(new BeginPromotionPaymentCommand(applied.ReservationId!.Value,o.Id,o.User));
        var writer=new PaymentAtomicWriter(database.Connection);
        var intent=await writer.ExecuteCreateIntentAsync(o.Id,$"order-reserve-{o.Id:N}",900,"IRR",[new(o.Wallet,900),new(database.FundingWallet,100,true)],null,null,default);
        using(var scope=database.Services.CreateScope())
        {var repo=scope.ServiceProvider.GetRequiredService<IOrderRepository>();var order=await repo.GetByIdWithItemsAsync(o.Id);order!.MarkAsReserved(intent.IntentId);await repo.UpdateAsync(order);}
        var options=await Send(new Refahi.Modules.Orders.Application.Contracts.Queries.GetOrderPaymentOptionsQuery(o.Id,o.User,"User"));
        Assert.True(options!.IsCovered);Assert.Equal(0,options.DeficitMinor);Assert.Equal(o.Wallet,Assert.Single(options.Allocations).WalletId);
        var alternative=Guid.NewGuid();await database.SeedWallet(alternative,o.User,2,1000);
        await Assert.ThrowsAsync<OrderStateConflictException>(()=>Send(new PayOrderCommand(o.Id,o.User,"User",[new(alternative,900)],"changed",ExpectedPricingVersion:2)));
        var paid=await Send(new PayOrderCommand(o.Id,o.User,"User",[new(o.Wallet,900)],"retry",ExpectedPricingVersion:2));Assert.Equal("Paid",paid.Status);
        Assert.Equal((1000L,0L),await database.Balance(alternative));Assert.Equal((0L,0L),await database.Balance(o.Wallet));
    }
}
