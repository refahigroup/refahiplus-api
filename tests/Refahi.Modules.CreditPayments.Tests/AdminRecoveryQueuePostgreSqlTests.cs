using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Application.Contracts.IntegrationEvents;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class AdminRecoveryQueuePostgreSqlTests : IAsyncLifetime
{
    private static readonly DateTimeOffset HistoricalTime = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Exact_order_lookup_and_recovery_queue_include_historical_payments_with_pagination()
    {
        await using var db = new CreditPaymentsDbContext(new DbContextOptionsBuilder<CreditPaymentsDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        await db.Database.MigrateAsync();
        var merchant = MerchantTestFactory.Create(Guid.NewGuid(), "پذیرنده", HistoricalTime);
        merchant.Activate(HistoricalTime);
        var terminal = merchant.AddTerminal("پایانه", "store", 1, 1_000_000,
            ["https://merchant.example/return"], HistoricalTime);
        db.Merchants.Add(merchant);
        Guid firstPaymentId = Guid.Empty;
        for (var i = 0; i < 2; i++)
        {
            var payment = GatewayPayment.Create(merchant.Id, terminal.Id, $"historical-order-{i}", 100,
                "IRR", null, "{}", "https://merchant.example/return", HistoricalTime.AddHours(1),
                merchant.DisplayName, terminal.Name, "store", HistoricalTime.AddMinutes(i));
            db.GatewayPayments.Add(payment);
            if (i == 0) firstPaymentId = payment.Id;
            var attempt = PaymentAttempt.Create(payment.Id, 1, PaymentAttemptTrigger.CustomerConfirmation,
                Guid.NewGuid(), "policy", "1", new string('a', 64), Guid.NewGuid(), HistoricalTime.AddMinutes(i));
            attempt.MarkPendingRecovery(HistoricalTime.AddMinutes(i + 1), HistoricalTime.AddMinutes(i + 2));
            db.PaymentAttempts.Add(attempt);
        }
        var checkoutId = Guid.NewGuid();
        db.CheckoutSessions.Add(CheckoutSession.Create(checkoutId, firstPaymentId, new byte[32],
            firstPaymentId.ToByteArray().Concat(checkoutId.ToByteArray()).ToArray(), 1,
            HistoricalTime.AddHours(1), HistoricalTime));
        var funding = FundingContinuation.Create(firstPaymentId, checkoutId, Guid.NewGuid(),
            Guid.NewGuid(), 50, HistoricalTime);
        funding.MarkPendingRecovery(HistoricalTime.AddMinutes(1), HistoricalTime.AddMinutes(2));
        db.FundingContinuations.Add(funding);
        var refund = Refund.Claim(firstPaymentId, terminal.Id, 10, "IRR", null, null, HistoricalTime);
        refund.MarkPendingRecovery(HistoricalTime.AddMinutes(1), HistoricalTime.AddMinutes(2));
        db.Refunds.Add(refund);
        var termination = PaymentTermination.Create(firstPaymentId, PaymentTerminationKind.Cancel,
            PaymentTerminationSource.Merchant, null, HistoricalTime);
        termination.MarkPendingRecovery(HistoricalTime.AddMinutes(1), HistoricalTime.AddMinutes(2));
        db.PaymentTerminations.Add(termination);
        await db.SaveChangesAsync();

        var reader = new OperationalAdminReader(db, new FixedClock(new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero)),
            Options.Create(new SettlementIntegrationOptions()));
        var exact = await reader.SearchPaymentsAsync(new SearchAdminPaymentsQuery(null, null, null,
            "historical-order-0", null, null, null, null, 1, 20), default);
        Assert.Equal(1, exact.TotalCount);
        Assert.Equal("historical-order-0", Assert.Single(exact.Items).MerchantOrderId);

        var queue = await reader.SearchRecoveryQueueAsync(new SearchAdminRecoveryQueueQuery("payment", 1, 1), default);
        Assert.Equal(2, queue.TotalCount);
        Assert.Single(queue.Items);
        Assert.Equal("payment", queue.Items[0].ResourceType);
        var second = await reader.SearchRecoveryQueueAsync(new SearchAdminRecoveryQueueQuery("payment", 2, 1), default);
        Assert.Single(second.Items);
        Assert.NotEqual(queue.Items[0].ResourceId, second.Items[0].ResourceId);
        foreach (var (type, id) in new[] { ("funding", funding.Id), ("refund", refund.Id), ("termination", termination.Id) })
        {
            var result = await reader.SearchRecoveryQueueAsync(new SearchAdminRecoveryQueueQuery(type, 1, 50), default);
            Assert.Equal(1, result.TotalCount);
            Assert.Equal(id, Assert.Single(result.Items).ResourceId);
            Assert.Equal(firstPaymentId, result.Items[0].PaymentId);
        }
        var summary = await reader.GetSummaryAsync(default);
        Assert.Equal(queue.TotalCount, summary.PendingPaymentRecoveries);
        Assert.Equal(1, summary.PendingFundingRecoveries);
        Assert.Equal(1, summary.PendingRefundRecoveries);
        Assert.Equal(1, summary.PendingTerminations);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
