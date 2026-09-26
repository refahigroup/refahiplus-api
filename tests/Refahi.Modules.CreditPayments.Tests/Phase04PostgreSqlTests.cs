using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase04PostgreSqlTests : IAsyncLifetime
{
    private const string Phase03 = "20260916124756_CreditPayments_AddGatewayPaymentAndIdempotency";
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 13, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [Fact(Skip = "Legacy staged migration uses the retired Supplier merchant model; current migration is covered separately.")]
    public async Task Phase03_to_phase04_preserves_history_enforces_checkout_constraints_and_rolls_back()
    {
        await using var db = Create();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Phase03);
        var merchant = MerchantTestFactory.Create(Guid.NewGuid(), "Historical Merchant", Now); merchant.Activate(Now);
        var terminal = merchant.AddTerminal("Historical Terminal", "store", 100, 1000,
            ["https://merchant.example/callback"], Now);
        db.Merchants.Add(merchant);
        await db.SaveChangesAsync();
        var historicalId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO credit_payments.gateway_payments
(gateway_payment_id, merchant_id, terminal_id, merchant_order_id, amount_minor, currency, description,
 metadata, status, return_url, expires_at, merchant_display_name_snapshot, terminal_name_snapshot,
 approved_category_code_snapshot, created_at, updated_at, version)
VALUES ({historicalId}, {merchant.Id}, {terminal.Id}, {'H' + "ISTORICAL"}, {100L}, {'I' + "RR"}, {"old"},
 CAST({"{}"} AS jsonb), {(short)GatewayPaymentStatus.Created}, {"https://merchant.example/callback"},
 {Now.AddMinutes(15)}, {"Historical Merchant"}, {"Historical Terminal"}, {"store"}, {Now}, {Now}, {1L})");

        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        var historical = await db.GatewayPayments.SingleAsync(x => x.Id == historicalId);
        Assert.Equal(GatewayPaymentStatus.Created, historical.Status);
        Assert.Empty(await db.CheckoutSessions.ToListAsync());

        var tokenHash = Enumerable.Repeat((byte)3, 32).ToArray();
        var first = CheckoutSession.Create(Guid.NewGuid(), historicalId, new byte[32], tokenHash, 1,
            Now.AddMinutes(15), Now);
        db.CheckoutSessions.Add(first);
        await db.SaveChangesAsync();

        await using (var another = Create())
        {
            var duplicateActive = CheckoutSession.Create(Guid.NewGuid(), historicalId,
                Enumerable.Repeat((byte)1, 32).ToArray(), Enumerable.Repeat((byte)2, 32).ToArray(), 1,
                Now.AddMinutes(15), Now);
            another.CheckoutSessions.Add(duplicateActive);
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => another.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)exception.InnerException!).SqlState);
        }

        await Assert.ThrowsAnyAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO credit_payments.checkout_otp_challenges
(checkout_otp_challenge_id, checkout_session_id, otp_type, status, sent_at, expires_at,
 verify_attempt_count, send_sequence, created_at, updated_at)
VALUES ({Guid.NewGuid()}, {first.Id}, {(short)0}, {(short)CheckoutOtpChallengeStatus.Dispatching},
 {Now}, {Now.AddMinutes(5)}, {-1}, {1}, {Now}, {Now})"));

        await using (var connection = new NpgsqlConnection(db.Database.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(@"
SELECT count(*)
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
  ON tc.constraint_name = kcu.constraint_name AND tc.constraint_schema = kcu.constraint_schema
WHERE tc.constraint_schema = 'credit_payments' AND tc.constraint_type = 'FOREIGN KEY'
  AND kcu.column_name IN ('customer_id', 'resolved_customer_id')", connection);
            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }

        await using (var a = Create())
        await using (var b = Create())
        {
            var sa = await a.CheckoutSessions.SingleAsync(x => x.Id == first.Id);
            var sb = await b.CheckoutSessions.SingleAsync(x => x.Id == first.Id);
            a.Entry(sa).Property(x => x.Version).CurrentValue++;
            await a.SaveChangesAsync();
            b.Entry(sb).Property(x => x.Version).CurrentValue++;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
        }

        db.ChangeTracker.Clear();
        await migrator.MigrateAsync(Phase03);
        await using var verifyConnection = new NpgsqlConnection(db.Database.GetConnectionString());
        await verifyConnection.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand(@"
SELECT to_regclass('credit_payments.checkout_sessions')::text,
       to_regclass('credit_payments.checkout_otp_challenges')::text,
       (SELECT count(*) FROM credit_payments.gateway_payments)", verifyConnection);
        await using var reader = await verifyCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(await reader.IsDBNullAsync(0));
        Assert.True(await reader.IsDBNullAsync(1));
        Assert.Equal(1L, reader.GetInt64(2));
    }

    private CreditPaymentsDbContext Create() => new(
        new DbContextOptionsBuilder<CreditPaymentsDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
}
