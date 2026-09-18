using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase10PostgreSqlTests : IAsyncLifetime
{
    private const string Phase09 = "20260917110218_CreditPayments_AddRefunds";
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 18, 30, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Phase09_to_phase10_preserves_data_creates_tables_and_rolls_back()
    {
        await Reset(Phase09);
        await using (var db = Create())
        {
            var merchant = Merchant.Create(Guid.NewGuid(), "migration", Now); merchant.Activate(Now);
            merchant.AddTerminal("terminal", "store", 1, 100, ["https://merchant.example/return"], Now);
            db.Merchants.Add(merchant); await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync();
        }
        await using (var verify = Create())
        {
            Assert.Single(await verify.Merchants.ToListAsync());
            Assert.Empty(await verify.WebhookEvents.ToListAsync());
            Assert.Empty(await verify.OutboxMessages.ToListAsync());
            Assert.Empty(await verify.WebhookDeliveries.ToListAsync());
        }
        await using (var rollback = Create()) await rollback.GetService<IMigrator>().MigrateAsync(Phase09);
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString()); await connection.OpenAsync();
        foreach (var table in new[] { "webhook_events", "outbox_messages", "webhook_deliveries" })
        {
            await using var command = new NpgsqlCommand($"SELECT to_regclass('credit_payments.{table}') IS NULL", connection);
            Assert.True((bool)(await command.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public async Task Payment_success_commits_event_and_outbox_atomically()
    {
        await Reset(); var graph = await InsertGraph(processing: true);
        await using var db = Create();
        await new CreditPaymentsRepository(db).FinalizePaymentSuccessAsync(graph.AttemptId!.Value,
            Guid.NewGuid(), Guid.NewGuid(), Now, Now, default);
        Assert.Equal(GatewayPaymentStatus.Succeeded, (await db.GatewayPayments.SingleAsync()).Status);
        var webhookEvent = await db.WebhookEvents.SingleAsync();
        Assert.Equal(WebhookEventTypes.PaymentSucceeded, webhookEvent.EventType);
        Assert.Contains(graph.PaymentId.ToString("D"), webhookEvent.PayloadJson);
        Assert.Single(await db.OutboxMessages.Where(x => x.WebhookEventId == webhookEvent.Id).ToListAsync());
    }

    [Fact]
    public async Task Event_constraint_failure_rolls_back_payment_success()
    {
        await Reset(); var graph = await InsertGraph(processing: true);
        await using (var seed = Create())
        {
            var existing = WebhookEvent.Create(Guid.NewGuid(),
                $"payment.succeeded:{graph.PaymentId:N}", WebhookEventTypes.PaymentSucceeded,
                graph.MerchantId, graph.TerminalId, "GatewayPayment", graph.PaymentId, Now, "{}", Now);
            seed.WebhookEvents.Add(existing); seed.OutboxMessages.Add(OutboxMessage.Create(existing.Id, Now));
            await seed.SaveChangesAsync();
        }
        await using (var db = Create())
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => new CreditPaymentsRepository(db)
                .FinalizePaymentSuccessAsync(graph.AttemptId!.Value, Guid.NewGuid(), Guid.NewGuid(), Now, Now, default));
        }
        await using var verify = Create();
        Assert.Equal(GatewayPaymentStatus.Processing, (await verify.GatewayPayments.SingleAsync()).Status);
        Assert.Single(await verify.WebhookEvents.ToListAsync());
    }

    [Fact]
    public async Task Event_constraint_failure_rolls_back_refund_success()
    {
        await Reset(); var graph = await InsertCaptured(); var refundId = await InsertRefund(graph, 1_000_000);
        await SeedConflictingEvent($"refund.succeeded:{refundId:N}", WebhookEventTypes.RefundSucceeded,
            graph, "Refund", refundId);
        await using (var db = Create())
            await Assert.ThrowsAsync<DbUpdateException>(() => new CreditPaymentsRepository(db)
                .FinalizeRefundSuccessAsync(refundId, Guid.NewGuid(), Now, Now, default));
        await using var verify = Create();
        Assert.Equal(RefundStatus.Processing, (await verify.Refunds.SingleAsync(x => x.Id == refundId)).Status);
        Assert.Equal(0, (await verify.GatewayPayments.SingleAsync(x => x.Id == graph.PaymentId)).RefundedAmountMinor);
    }

    [Fact]
    public async Task Event_constraint_failure_rolls_back_cancel()
    {
        await Reset(); var graph = await InsertGraph();
        await SeedConflictingEvent($"payment.canceled:{graph.PaymentId:N}", WebhookEventTypes.PaymentCanceled,
            graph, "GatewayPayment", graph.PaymentId);
        await using (var db = Create())
            await Assert.ThrowsAsync<DbUpdateException>(() => new CreditPaymentsRepository(db)
                .RequestMerchantCancellationAsync(graph.PaymentId, graph.MerchantId, graph.TerminalId,
                    "atomic-cancel", CancelGatewayPaymentHandler.Hash(graph.PaymentId, null), null, Now, default));
        await using var verify = Create();
        Assert.Equal(GatewayPaymentStatus.AwaitingCustomer,
            (await verify.GatewayPayments.SingleAsync(x => x.Id == graph.PaymentId)).Status);
        Assert.Empty(await verify.PaymentTerminations.ToListAsync());
    }

    [Fact]
    public async Task Event_constraint_failure_rolls_back_expire()
    {
        await Reset(); var graph = await InsertGraph(expiresAt: Now.AddMinutes(1));
        await SeedConflictingEvent($"payment.expired:{graph.PaymentId:N}", WebhookEventTypes.PaymentExpired,
            graph, "GatewayPayment", graph.PaymentId);
        await using (var db = Create())
            await Assert.ThrowsAsync<DbUpdateException>(() => new CreditPaymentsRepository(db)
                .ClaimDueExpirationsAsync(Now.AddMinutes(2), 10, default));
        await using var verify = Create();
        Assert.Equal(GatewayPaymentStatus.AwaitingCustomer,
            (await verify.GatewayPayments.SingleAsync(x => x.Id == graph.PaymentId)).Status);
        Assert.Empty(await verify.PaymentTerminations.ToListAsync());
    }

    [Fact]
    public async Task Cancel_and_expire_create_exact_terminal_events()
    {
        await Reset(); var cancel = await InsertGraph();
        await using (var db = Create())
        {
            var hash = CancelGatewayPaymentHandler.Hash(cancel.PaymentId, "cancel");
            await new CreditPaymentsRepository(db).RequestMerchantCancellationAsync(cancel.PaymentId,
                cancel.MerchantId, cancel.TerminalId, "phase10-cancel", hash, "cancel", Now, default);
        }
        var expire = await InsertGraph(expiresAt: Now.AddMinutes(1));
        await using (var db = Create())
            await new CreditPaymentsRepository(db).ClaimDueExpirationsAsync(Now.AddMinutes(2), 10, default);
        await using var verify = Create();
        Assert.Single(await verify.WebhookEvents.Where(x => x.EventType == WebhookEventTypes.PaymentCanceled).ToListAsync());
        Assert.Single(await verify.WebhookEvents.Where(x => x.EventType == WebhookEventTypes.PaymentExpired).ToListAsync());
        Assert.Equal(2, await verify.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Refund_success_and_definitive_failure_create_events_but_pending_does_not()
    {
        await Reset(); var success = await InsertCaptured(); var successfulRefund = await InsertRefund(success, 1_000_000);
        await using (var db = Create()) await new CreditPaymentsRepository(db).FinalizeRefundSuccessAsync(
            successfulRefund, Guid.NewGuid(), Now, Now, default);
        var failure = await InsertCaptured(); var failedRefund = await InsertRefund(failure, 1_000_000);
        await using (var db = Create()) await new CreditPaymentsRepository(db).MarkRefundFailedAsync(
            failedRefund, "wallet_not_refundable", Now, default);
        var pending = await InsertCaptured(); var pendingRefund = await InsertRefund(pending, 1_000_000);
        await using (var db = Create()) await new CreditPaymentsRepository(db).MarkRefundPendingRecoveryAsync(
            pendingRefund, Now, Now.AddMinutes(1), default);
        await using var verify = Create();
        Assert.Single(await verify.WebhookEvents.Where(x => x.EventType == WebhookEventTypes.RefundSucceeded).ToListAsync());
        Assert.Single(await verify.WebhookEvents.Where(x => x.EventType == WebhookEventTypes.RefundFailed).ToListAsync());
        Assert.DoesNotContain(await verify.WebhookEvents.ToListAsync(), x => x.AggregateId == pendingRefund);
    }

    [Fact]
    public async Task Two_outbox_and_delivery_workers_have_one_logical_owner()
    {
        await Reset(); var graph = await InsertGraph(processing: true);
        await using (var db = Create()) await new CreditPaymentsRepository(db).FinalizePaymentSuccessAsync(
            graph.AttemptId!.Value, null, Guid.NewGuid(), Now, Now, default);
        async Task<int> Dispatch(string owner)
        { await using var db = Create(); return await new WebhookWorkService(db).DispatchAsync(owner, Now, Now.AddMinutes(1), 10, default); }
        var dispatched = await Task.WhenAll(Dispatch("a"), Dispatch("b"));
        Assert.Equal(1, dispatched.Sum());
        async Task<IReadOnlyList<Guid>> Lease(string owner)
        { await using var db = Create(); return await new WebhookWorkService(db).LeaseDeliveriesAsync(owner, Now, Now.AddMinutes(1), 10, default); }
        var leased = await Task.WhenAll(Lease("a"), Lease("b"));
        Assert.Equal(1, leased.Sum(x => x.Count));
        await using var verify = Create();
        Assert.Single(await verify.WebhookDeliveries.ToListAsync());
        Assert.Equal(OutboxMessageStatus.Completed, (await verify.OutboxMessages.SingleAsync()).Status);
    }

    [Fact]
    public async Task Dead_letter_manual_resend_reuses_event_and_payload()
    {
        await Reset(); var graph = await InsertGraph(processing: true);
        await using (var db = Create()) await new CreditPaymentsRepository(db).FinalizePaymentSuccessAsync(
            graph.AttemptId!.Value, null, Guid.NewGuid(), Now, Now, default);
        Guid deliveryId; Guid eventId; string payload;
        await using (var db = Create())
        {
            var work = new WebhookWorkService(db); await work.DispatchAsync("dispatcher", Now, Now.AddMinutes(1), 10, default);
            deliveryId = Assert.Single(await work.LeaseDeliveriesAsync("worker", Now, Now.AddMinutes(1), 10, default));
            var item = (await work.GetWorkAsync(deliveryId, default))!; eventId = item.Event.Id; payload = item.Event.PayloadJson;
            await work.CompleteFailureAsync(deliveryId, "worker", "http_500", 500, 1,
                Now, Now.AddMinutes(1), 1, default);
        }
        await using (var db = Create()) Assert.True(await new CreditPaymentsRepository(db)
            .ResendDeadLetterAsync(eventId, "admin", "endpoint fixed", Now.AddMinutes(2), default));
        await using var verify = Create();
        var delivery = await verify.WebhookDeliveries.SingleAsync();
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(1, delivery.ManualResendCount);
        Assert.Equal(eventId, delivery.WebhookEventId);
        Assert.Equal(payload, (await verify.WebhookEvents.SingleAsync()).PayloadJson);
    }

    [Fact]
    public async Task Merchant_received_success_but_local_save_failed_retries_same_event()
    {
        await Reset(); var graph = await InsertGraph(processing: true);
        await using (var db = Create()) await new CreditPaymentsRepository(db).FinalizePaymentSuccessAsync(
            graph.AttemptId!.Value, null, Guid.NewGuid(), Now, Now, default);
        Guid eventId; string payload; Guid deliveryId;
        await using (var db = Create())
        {
            var work = new WebhookWorkService(db); await work.DispatchAsync("dispatcher", Now, Now.AddMinutes(1), 10, default);
            deliveryId = Assert.Single(await work.LeaseDeliveriesAsync("worker-a", Now, Now.AddSeconds(1), 10, default));
            var first = (await work.GetWorkAsync(deliveryId, default))!;
            eventId = first.Event.Id; payload = first.Event.PayloadJson;
            // Simulate HTTP 2xx followed by process death: CompleteSuccessAsync is intentionally not called.
        }
        await using (var db = Create())
        {
            var work = new WebhookWorkService(db);
            Assert.Equal(deliveryId, Assert.Single(await work.LeaseDeliveriesAsync(
                "worker-b", Now.AddSeconds(2), Now.AddMinutes(1), 10, default)));
            var retry = (await work.GetWorkAsync(deliveryId, default))!;
            Assert.Equal(eventId, retry.Event.Id); Assert.Equal(payload, retry.Event.PayloadJson);
            Assert.Equal(2, retry.AttemptCount);
        }
    }

    [Fact]
    public async Task Pending_retry_resolves_current_rotated_endpoint_without_mutating_event()
    {
        await Reset(); var graph = await InsertGraph(processing: true, withWebhook: true);
        await using (var db = Create()) await new CreditPaymentsRepository(db).FinalizePaymentSuccessAsync(
            graph.AttemptId!.Value, null, Guid.NewGuid(), Now, Now, default);
        Guid deliveryId; Guid eventId; string payload;
        await using (var db = Create())
        {
            var work = new WebhookWorkService(db); await work.DispatchAsync("dispatcher", Now, Now.AddMinutes(1), 10, default);
            deliveryId = Assert.Single(await work.LeaseDeliveriesAsync("worker", Now, Now.AddMinutes(1), 10, default));
            var original = (await work.GetWorkAsync(deliveryId, default))!;
            eventId = original.Event.Id; payload = original.Event.PayloadJson;
        }
        await using (var db = Create())
        {
            var terminal = await db.Terminals.Include(x => x.WebhookEndpoint).SingleAsync(x => x.Id == graph.TerminalId);
            terminal.ConfigureWebhook("https://rotated.example/hook", "cipher-v2", 2, Now.AddSeconds(1));
            await db.SaveChangesAsync();
        }
        await using (var db = Create())
        {
            var current = (await new WebhookWorkService(db).GetWorkAsync(deliveryId, default))!;
            Assert.Equal(2, current.Endpoint!.SigningSecretVersion);
            Assert.Equal("https://rotated.example/hook", current.Endpoint.Url.TrimEnd('/'));
            Assert.Equal(eventId, current.Event.Id); Assert.Equal(payload, current.Event.PayloadJson);
        }
    }

    [Fact]
    public async Task Historical_reconciliation_is_idempotent_and_does_not_change_money()
    {
        await Reset(); var graph = await InsertCaptured();
        await using (var db = Create())
        {
            var reconciler = new WebhookEventReconciler(db, new FixedClock(Now.AddDays(1)));
            Assert.Equal(1, await reconciler.ExecuteOnceAsync(50, default));
        }
        await using (var db = Create())
        {
            var reconciler = new WebhookEventReconciler(db, new FixedClock(Now.AddDays(1)));
            Assert.Equal(0, await reconciler.ExecuteOnceAsync(50, default));
        }
        await using var verify = Create();
        Assert.Single(await verify.WebhookEvents.ToListAsync()); Assert.Single(await verify.OutboxMessages.ToListAsync());
        var payment = await verify.GatewayPayments.SingleAsync();
        Assert.Equal(graph.WalletPaymentId, payment.WalletPaymentId); Assert.Equal(10_000_000, payment.CapturedAmountMinor);
    }

    private async Task<Graph> InsertGraph(bool processing = false, DateTimeOffset? expiresAt = null,
        bool withWebhook = false)
    {
        await using var db = Create();
        var merchant = Merchant.Create(Guid.NewGuid(), $"merchant-{Guid.NewGuid():N}", Now); merchant.Activate(Now);
        var terminal = merchant.AddTerminal($"terminal-{Guid.NewGuid():N}", "store", 1, 20_000_000,
            ["https://merchant.example/return"], Now);
        if (withWebhook) terminal.ConfigureWebhook("https://hooks.example/hook", "cipher-v1", 1, Now);
        db.Merchants.Add(merchant); await db.SaveChangesAsync();
        var payment = GatewayPayment.Create(merchant.Id, terminal.Id, $"order-{Guid.NewGuid():N}", 10_000_000,
            "IRR", null, "{}", "https://merchant.example/return", expiresAt ?? Now.AddMinutes(10),
            merchant.DisplayName, terminal.Name, "store", Now);
        var customer = Guid.NewGuid(); var order = Guid.NewGuid(); var checkoutId = Guid.NewGuid();
        payment.ActivateCheckout(Now); payment.BindCustomer(customer, Now); payment.BindOrder(order, Now);
        var session = CheckoutSession.Create(checkoutId, payment.Id, new byte[32],
            payment.Id.ToByteArray().Concat(checkoutId.ToByteArray()).ToArray(), 1,
            expiresAt ?? Now.AddMinutes(10), Now);
        session.Authenticate(customer, Enumerable.Repeat((byte)7, 32).ToArray(),
            expiresAt ?? Now.AddMinutes(10), Now);
        db.GatewayPayments.Add(payment); db.CheckoutSessions.Add(session); await db.SaveChangesAsync();
        Guid? attempt = null;
        if (processing)
        {
            var claim = await new CreditPaymentsRepository(db).ClaimPaymentExecutionAsync(payment.Id,
                PaymentAttemptTrigger.CustomerConfirmation, "policy", "1", new string('a', 64),
                Guid.NewGuid(), Now, default);
            attempt = claim.Context.ActiveAttempt!.Id;
        }
        return new(payment.Id, merchant.Id, terminal.Id, attempt, null);
    }

    private async Task<Graph> InsertCaptured()
    {
        var graph = await InsertGraph(processing: true); var wallet = Guid.NewGuid();
        await using var db = Create();
        var payment = await db.GatewayPayments.SingleAsync(x => x.Id == graph.PaymentId);
        var attempt = await db.PaymentAttempts.SingleAsync(x => x.Id == graph.AttemptId);
        var session = await db.CheckoutSessions.SingleAsync(x => x.GatewayPaymentId == graph.PaymentId);
        payment.MarkSucceeded(wallet, Now, Now); attempt.MarkSucceeded(null, wallet, Now); session.Complete(Now);
        await db.SaveChangesAsync(); return graph with { WalletPaymentId = wallet };
    }

    private async Task<Guid> InsertRefund(Graph graph, long amount)
    {
        await using var db = Create(); var refund = Refund.Claim(graph.PaymentId, graph.TerminalId,
        amount, "IRR", null, null, Now); db.Refunds.Add(refund); await db.SaveChangesAsync(); return refund.Id;
    }

    private async Task SeedConflictingEvent(string sourceKey, string eventType, Graph graph,
        string aggregateType, Guid aggregateId)
    {
        await using var db = Create();
        var existing = WebhookEvent.Create(Guid.NewGuid(), sourceKey, eventType,
            graph.MerchantId, graph.TerminalId, aggregateType, aggregateId, Now, "{}", Now);
        db.WebhookEvents.Add(existing); db.OutboxMessages.Add(OutboxMessage.Create(existing.Id, Now));
        await db.SaveChangesAsync();
    }

    private async Task Reset(string? target = null)
    { await using var db = Create(); var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("0"); await migrator.MigrateAsync(target); }
    private CreditPaymentsDbContext Create() => new(new DbContextOptionsBuilder<CreditPaymentsDbContext>()
        .UseNpgsql(postgres.GetConnectionString()).Options);
    private sealed record Graph(Guid PaymentId, Guid MerchantId, Guid TerminalId, Guid? AttemptId, Guid? WalletPaymentId);
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
