using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;
using Refahi.Modules.CreditPayments.Application.Contracts.IntegrationEvents;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase13PostgreSqlTests : IAsyncLifetime
{
    private const string Phase12 = "20260917205545_CreditPayments_AddReconciliationAndIntegrity";
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 5, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [Fact(Skip = "Legacy staged migration uses the retired Supplier merchant model; current migration is covered separately.")]
    public async Task Phase12_to_phase13_preserves_financial_data_and_rolls_back_table()
    {
        await Reset(Phase12);
        await using (var db = Create())
        {
            var merchant = MerchantTestFactory.Create(Guid.NewGuid(), "migration", Now); merchant.Activate(Now);
            merchant.AddTerminal("terminal", "store", 1, 100, ["https://merchant.example/return"], Now);
            db.Merchants.Add(merchant); await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync();
        }
        await using (var verify = Create())
        {
            Assert.Single(await verify.Merchants.ToListAsync());
            Assert.Empty(await verify.IntegrationEvents.ToListAsync());
        }
        await using (var rollback = Create()) await rollback.GetService<IMigrator>().MigrateAsync(Phase12);
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString()); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT to_regclass('credit_payments.integration_events') IS NULL", connection);
        Assert.True((bool)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task New_capture_commits_financial_webhook_and_settlement_fact_atomically()
    {
        await Reset(); var graph = await InsertGraph(processing: true);
        var walletPaymentId = Guid.NewGuid();
        await using (var db = Create())
            await new CreditPaymentsRepository(db).FinalizePaymentSuccessAsync(graph.AttemptId!.Value,
                null, walletPaymentId, Now, Now, default);
        await using var verify = Create();
        Assert.Equal(GatewayPaymentStatus.Succeeded, (await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Single(await verify.WebhookEvents.ToListAsync());
        Assert.Single(await verify.OutboxMessages.ToListAsync());
        var fact = await verify.IntegrationEvents.SingleAsync();
        Assert.Equal(CreditPaymentSettlementEventTypes.CapturedV1, fact.EventType);
        using var json = JsonDocument.Parse(fact.PayloadJson);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("supplierId").ValueKind);
        Assert.Equal(walletPaymentId, json.RootElement.GetProperty("walletPaymentId").GetGuid());
        Assert.Equal(10_000_000, json.RootElement.GetProperty("capturedAmountMinor").GetInt64());
        Assert.False(json.RootElement.TryGetProperty("customerId", out _));
    }

    [Fact]
    public async Task Settlement_fact_constraint_failure_rolls_back_capture_and_merchant_webhook()
    {
        await Reset(); var graph = await InsertGraph(processing: true);
        await SeedConflict($"credit-payment-captured:{graph.PaymentId:N}",
            CreditPaymentSettlementEventTypes.CapturedV1, "GatewayPayment", graph.PaymentId);
        await using (var db = Create())
            await Assert.ThrowsAsync<DbUpdateException>(() => new CreditPaymentsRepository(db)
                .FinalizePaymentSuccessAsync(graph.AttemptId!.Value, null, Guid.NewGuid(), Now, Now, default));
        await using var verify = Create();
        Assert.Equal(GatewayPaymentStatus.Processing, (await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Empty(await verify.WebhookEvents.ToListAsync());
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
        Assert.Single(await verify.IntegrationEvents.ToListAsync());
    }

    [Fact]
    public async Task Settlement_fact_constraint_failure_rolls_back_refund_and_merchant_webhook()
    {
        await Reset(); var graph = await InsertCapturedDirect(); var refundId = await InsertRefund(graph, 2_000_000);
        await SeedConflict($"credit-payment-refunded:{refundId:N}",
            CreditPaymentSettlementEventTypes.RefundedV1, "Refund", refundId);
        await using (var db = Create())
            await Assert.ThrowsAsync<DbUpdateException>(() => new CreditPaymentsRepository(db)
                .FinalizeRefundSuccessAsync(refundId, Guid.NewGuid(), Now, Now, default));
        await using var verify = Create();
        Assert.Equal(RefundStatus.Processing, (await verify.Refunds.SingleAsync()).Status);
        Assert.Equal(0, (await verify.GatewayPayments.SingleAsync()).RefundedAmountMinor);
        Assert.Empty(await verify.WebhookEvents.ToListAsync());
    }

    [Fact]
    public async Task Multiple_refunds_create_one_fact_each_with_cumulative_and_full_state()
    {
        await Reset(); var graph = await InsertCapturedDirect();
        var expected = new[] { (2_000_000L, 2_000_000L, false), (3_000_000L, 5_000_000L, false),
            (5_000_000L, 10_000_000L, true) };
        foreach (var item in expected)
        {
            var refundId = await InsertRefund(graph, item.Item1);
            await using var db = Create();
            await new CreditPaymentsRepository(db).FinalizeRefundSuccessAsync(refundId, Guid.NewGuid(), Now, Now, default);
        }
        await using var verify = Create();
        var facts = await verify.IntegrationEvents.Where(x => x.EventType == CreditPaymentSettlementEventTypes.RefundedV1)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync();
        Assert.Equal(3, facts.Count);
        var actual = facts.Select(x =>
        {
            using var json = JsonDocument.Parse(x.PayloadJson);
            return (json.RootElement.GetProperty("refundAmountMinor").GetInt64(),
                json.RootElement.GetProperty("cumulativeRefundedAmountMinor").GetInt64(),
                json.RootElement.GetProperty("isFullyRefunded").GetBoolean());
        }).OrderBy(x => x.Item2).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Failed_refund_and_canceled_payment_create_no_settlement_fact()
    {
        await Reset(); var captured = await InsertCapturedDirect(); var refundId = await InsertRefund(captured, 1_000_000);
        await using (var db = Create()) await new CreditPaymentsRepository(db).MarkRefundFailedAsync(refundId, "failed", Now, default);
        var cancel = await InsertGraph();
        await using (var db = Create()) await new CreditPaymentsRepository(db).RequestMerchantCancellationAsync(
            cancel.PaymentId, cancel.MerchantId, cancel.TerminalId, "phase13-cancel",
            Refahi.Modules.CreditPayments.Application.CancelGatewayPaymentHandler.Hash(cancel.PaymentId, null), null, Now, default);
        await using var verify = Create();
        Assert.Empty(await verify.IntegrationEvents.ToListAsync());
    }

    [Fact]
    public async Task Historical_reconciliation_is_idempotent_and_preserves_occurrence_time()
    {
        await Reset(); var graph = await InsertCapturedDirect(); var refundId = await InsertRefund(graph, 2_000_000);
        var walletRefundId = Guid.NewGuid();
        await using (var db = Create())
        {
            var payment = await db.GatewayPayments.SingleAsync(x => x.Id == graph.PaymentId);
            var refund = await db.Refunds.SingleAsync(x => x.Id == refundId);
            payment.ApplySuccessfulRefund(refund.AmountMinor, Now.AddMinutes(2));
            refund.MarkSucceeded(walletRefundId, false, Now.AddMinutes(2), Now.AddMinutes(2));
            await db.SaveChangesAsync();
        }
        await using (var db = Create()) Assert.Equal(2,
            await new SettlementIntegrationEventReconciler(db, new FixedClock(Now.AddDays(1))).ExecuteOnceAsync(50, default));
        await using (var db = Create()) Assert.Equal(0,
            await new SettlementIntegrationEventReconciler(db, new FixedClock(Now.AddDays(1))).ExecuteOnceAsync(50, default));
        await using var verify = Create();
        Assert.Equal(2, await verify.IntegrationEvents.CountAsync());
        Assert.Equal(Now, (await verify.IntegrationEvents.SingleAsync(x => x.EventType == CreditPaymentSettlementEventTypes.CapturedV1)).OccurredAt);
        Assert.Equal(Now.AddMinutes(2), (await verify.IntegrationEvents.SingleAsync(x => x.EventType == CreditPaymentSettlementEventTypes.RefundedV1)).OccurredAt);
        Assert.Equal(2_000_000, (await verify.GatewayPayments.SingleAsync()).RefundedAmountMinor);
    }

    [Fact]
    public async Task Two_reconcilers_and_live_finalization_keep_one_logical_capture_fact()
    {
        await Reset(); var graph = await InsertGraph(processing: true);
        async Task Finalize()
        {
            await using var db = Create(); await new CreditPaymentsRepository(db).FinalizePaymentSuccessAsync(
                graph.AttemptId!.Value, null, Guid.NewGuid(), Now, Now, default);
        }
        async Task Reconcile()
        {
            await using var db = Create(); await new SettlementIntegrationEventReconciler(db,
                new FixedClock(Now.AddMinutes(1))).ExecuteOnceAsync(50, default);
        }
        await Task.WhenAll(Finalize(), Reconcile(), Reconcile());
        await using var verify = Create();
        Assert.Single(await verify.IntegrationEvents.Where(x => x.EventType == CreditPaymentSettlementEventTypes.CapturedV1).ToListAsync());
    }

    [Fact]
    public async Task Two_publishers_have_one_normal_lease_owner()
    {
        await Reset(); await InsertPendingEvent();
        async Task<IReadOnlyList<Guid>> Lease(string owner)
        {
            await using var db = Create(); return await Work(db, new RecordingPublisher(), new FixedClock(Now)).LeaseBatchAsync(owner, default);
        }
        var leased = await Task.WhenAll(Lease("worker-a"), Lease("worker-b"));
        Assert.Equal(1, leased.Sum(x => x.Count));
    }

    [Fact]
    public async Task Publisher_crash_before_publish_releases_lease_and_reuses_same_event()
    {
        await Reset(); var eventId = await InsertPendingEvent(); var transport = new RecordingPublisher();
        await using (var db = Create()) Assert.Equal(eventId,
            Assert.Single(await Work(db, transport, new FixedClock(Now)).LeaseBatchAsync("worker-a", default)));
        var later = new FixedClock(Now.AddMinutes(2));
        await using (var db = Create())
        {
            var work = Work(db, transport, later);
            Assert.Equal(eventId, Assert.Single(await work.LeaseBatchAsync("worker-b", default)));
            await work.PublishOneAsync(eventId, "worker-b", default);
        }
        Assert.Single(transport.Messages);
        Assert.Equal(eventId, transport.Messages[0].EventId);
    }

    [Fact]
    public async Task Transport_acceptance_then_reported_failure_republishes_same_immutable_message()
    {
        await Reset(); var eventId = await InsertPendingEvent(); var transport = new RecordingPublisher { ThrowAfterAccept = true };
        await using (var db = Create())
        {
            var work = Work(db, transport, new FixedClock(Now));
            Assert.Equal(eventId, Assert.Single(await work.LeaseBatchAsync("worker-a", default)));
            await work.PublishOneAsync(eventId, "worker-a", default);
        }
        transport.ThrowAfterAccept = false;
        await using (var db = Create())
        {
            var work = Work(db, transport, new FixedClock(Now.AddSeconds(2)));
            Assert.Equal(eventId, Assert.Single(await work.LeaseBatchAsync("worker-b", default)));
            await work.PublishOneAsync(eventId, "worker-b", default);
        }
        Assert.Equal(2, transport.Messages.Count);
        Assert.Equal(transport.Messages[0], transport.Messages[1]);
        await using var verify = Create();
        Assert.Equal(CreditPaymentIntegrationEventStatus.Published, (await verify.IntegrationEvents.SingleAsync()).Status);
    }

    private CreditPaymentIntegrationEventWorkService Work(CreditPaymentsDbContext db,
        ICreditPaymentIntegrationEventPublisher transport, TimeProvider clock) => new(db, transport,
        Options.Create(new SettlementIntegrationOptions { BatchSize = 50, LeaseSeconds = 60,
            BaseRetrySeconds = 1, MaximumRetrySeconds = 1 }), clock);

    private async Task<Graph> InsertGraph(bool processing = false)
    {
        await using var db = Create();
        var merchant = MerchantTestFactory.Create(Guid.NewGuid(), $"merchant-{Guid.NewGuid():N}", Now); merchant.Activate(Now);
        var terminal = merchant.AddTerminal($"terminal-{Guid.NewGuid():N}", "store", 1, 20_000_000,
            ["https://merchant.example/return"], Now);
        db.Merchants.Add(merchant); await db.SaveChangesAsync();
        var payment = GatewayPayment.Create(merchant.Id, terminal.Id, $"order-{Guid.NewGuid():N}", 10_000_000,
            "IRR", null, "{}", "https://merchant.example/return", Now.AddMinutes(10),
            merchant.DisplayName, terminal.Name, "store", Now);
        var customer = Guid.NewGuid(); var order = Guid.NewGuid(); var checkoutId = Guid.NewGuid();
        payment.ActivateCheckout(Now); payment.BindCustomer(customer, Now); payment.BindOrder(order, Now);
        var session = CheckoutSession.Create(checkoutId, payment.Id, new byte[32],
            payment.Id.ToByteArray().Concat(checkoutId.ToByteArray()).ToArray(), 1, Now.AddMinutes(10), Now);
        session.Authenticate(customer, Enumerable.Repeat((byte)7, 32).ToArray(), Now.AddMinutes(10), Now);
        db.GatewayPayments.Add(payment); db.CheckoutSessions.Add(session); await db.SaveChangesAsync();
        Guid? attemptId = null;
        if (processing)
        {
            var claim = await new CreditPaymentsRepository(db).ClaimPaymentExecutionAsync(payment.Id,
                PaymentAttemptTrigger.CustomerConfirmation, "policy", "1", new string('a', 64),
                Guid.NewGuid(), Now, default);
            attemptId = claim.Context.ActiveAttempt!.Id;
        }
        return new(payment.Id, merchant.Id, terminal.Id, attemptId);
    }

    private async Task<Graph> InsertCapturedDirect()
    {
        var graph = await InsertGraph(processing: true); var wallet = Guid.NewGuid();
        await using var db = Create(); var payment = await db.GatewayPayments.SingleAsync(x => x.Id == graph.PaymentId);
        var attempt = await db.PaymentAttempts.SingleAsync(x => x.Id == graph.AttemptId);
        var session = await db.CheckoutSessions.SingleAsync(x => x.GatewayPaymentId == graph.PaymentId);
        payment.MarkSucceeded(wallet, Now, Now); attempt.MarkSucceeded(null, wallet, Now); session.Complete(Now);
        await db.SaveChangesAsync(); return graph;
    }

    private async Task<Guid> InsertRefund(Graph graph, long amount)
    {
        await using var db = Create(); var refund = Refund.Claim(graph.PaymentId, graph.TerminalId,
            amount, "IRR", $"rf-{Guid.NewGuid():N}", null, Now);
        db.Refunds.Add(refund); await db.SaveChangesAsync(); return refund.Id;
    }

    private async Task SeedConflict(string sourceKey, string eventType, string aggregateType, Guid aggregateId)
    {
        await using var db = Create(); db.IntegrationEvents.Add(CreditPaymentIntegrationEvent.Create(
            Guid.NewGuid(), sourceKey, eventType, 1, CreditPaymentSettlementEventTypes.Source,
            aggregateType, aggregateId, Now, "{}", Now)); await db.SaveChangesAsync();
    }

    private async Task<Guid> InsertPendingEvent()
    {
        await using var db = Create(); var id = Guid.NewGuid();
        db.IntegrationEvents.Add(CreditPaymentIntegrationEvent.Create(id, $"credit-payment-captured:{Guid.NewGuid():N}",
            CreditPaymentSettlementEventTypes.CapturedV1, 1, CreditPaymentSettlementEventTypes.Source,
            "GatewayPayment", Guid.NewGuid(), Now, "{\"immutable\":true}", Now));
        await db.SaveChangesAsync(); return id;
    }

    private async Task Reset(string? migration = null)
    { await using var db = Create(); var m = db.GetService<IMigrator>(); await m.MigrateAsync("0"); await m.MigrateAsync(migration); }
    private CreditPaymentsDbContext Create() => new(new DbContextOptionsBuilder<CreditPaymentsDbContext>()
        .UseNpgsql(postgres.GetConnectionString()).Options);
    private sealed record Graph(Guid PaymentId, Guid MerchantId, Guid TerminalId, Guid? AttemptId);
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class RecordingPublisher : ICreditPaymentIntegrationEventPublisher
    {
        public bool ThrowAfterAccept { get; set; }
        public List<CreditPaymentIntegrationEventMessage> Messages { get; } = [];
        public Task PublishAsync(CreditPaymentIntegrationEventMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            if (ThrowAfterAccept) throw new TimeoutException("accepted but acknowledgement was lost");
            return Task.CompletedTask;
        }
    }
}
