using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using Refahi.Modules.Wallets.Domain.Enums;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Context;
using Xunit;

namespace Refahi.Modules.Wallets.Tests;

public sealed class PaymentAtomicWriterPostgresTests
{
    private const string ConnectionVariable = "REFAHI_WALLETS_TEST_CONNECTION";

    [Fact]
    public async Task PaymentLifecycle_IsAtomicIdempotentAndPersistsRelationType()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains("test", connectionBuilder.Database, StringComparison.OrdinalIgnoreCase);

        await WaitForDatabaseAsync(connectionString);
        await ResetAndMigrateAsync(connectionString);

        var firstWalletId = Guid.NewGuid();
        var secondWalletId = Guid.NewGuid();
        await SeedWalletsAsync(connectionString, firstWalletId, secondWalletId);

        var writer = new PaymentAtomicWriter(connectionString);
        var allocations = new List<AllocationInput>
        {
            new(firstWalletId, 600),
            new(secondWalletId, 400)
        };

        var orderId = Guid.NewGuid();
        const string metadataJson = "{\"source\":\"integration-test\"}";
        var reserve = await writer.ExecuteCreateIntentAsync(
            orderId, "reserve-key", 1_000, "IRR", allocations, metadataJson, CancellationToken.None);
        var reserveReplay = await writer.ExecuteCreateIntentAsync(
            orderId, "reserve-key", 1_000, "IRR", allocations, metadataJson, CancellationToken.None);

        Assert.Equal(CreateIntentOutcome.Created, reserve.Outcome);
        Assert.Equal(CreateIntentOutcome.CreatedCached, reserveReplay.Outcome);
        Assert.Equal(TimeSpan.Zero, reserveReplay.CreatedAt.Offset);
        AssertTimestampsMatch(reserve.CreatedAt, reserveReplay.CreatedAt);
        await AssertBalancesAsync(connectionString, (firstWalletId, 400, 600), (secondWalletId, 600, 400));
        await AssertLedgerAsync(connectionString, operationType: (short)OperationType.Reserve, expectedCount: 2);

        var capture = await writer.ExecuteCaptureIntentAsync(reserve.IntentId, "capture-key", CancellationToken.None);
        var captureReplay = await writer.ExecuteCaptureIntentAsync(reserve.IntentId, "capture-key", CancellationToken.None);

        Assert.Equal(CaptureIntentOutcome.Captured, capture.Outcome);
        Assert.Equal(CaptureIntentOutcome.CapturedCached, captureReplay.Outcome);
        Assert.Equal(capture.PaymentId, captureReplay.PaymentId);
        Assert.Equal(TimeSpan.Zero, captureReplay.CompletedAt.Offset);
        AssertTimestampsMatch(capture.CompletedAt, captureReplay.CompletedAt);
        await AssertBalancesAsync(connectionString, (firstWalletId, 400, 0), (secondWalletId, 600, 0));
        await AssertLedgerAsync(connectionString, operationType: (short)OperationType.Payment, expectedCount: 2);

        var refund = await writer.ExecuteRefundPaymentAsync(
            capture.PaymentId, "refund-key", "integration test", null, CancellationToken.None);
        var refundReplay = await writer.ExecuteRefundPaymentAsync(
            capture.PaymentId, "refund-key", "integration test", null, CancellationToken.None);

        Assert.Equal(RefundPaymentOutcome.Refunded, refund.Outcome);
        Assert.Equal(RefundPaymentOutcome.RefundedCached, refundReplay.Outcome);
        Assert.Equal(refund.RefundId, refundReplay.RefundId);
        Assert.Equal(TimeSpan.Zero, refundReplay.CompletedAt.Offset);
        AssertTimestampsMatch(refund.CompletedAt, refundReplay.CompletedAt);
        await AssertBalancesAsync(connectionString, (firstWalletId, 1_000, 0), (secondWalletId, 1_000, 0));
        await AssertLedgerAsync(connectionString, operationType: (short)OperationType.Refund, expectedCount: 2);

        var releaseIntent = await writer.ExecuteCreateIntentAsync(
            Guid.NewGuid(), "release-reserve-key", 300, "IRR",
            [new(firstWalletId, 100), new(secondWalletId, 200)], null, CancellationToken.None);
        var release = await writer.ExecuteReleaseIntentAsync(
            releaseIntent.IntentId, "release-key", CancellationToken.None);
        var releaseReplay = await writer.ExecuteReleaseIntentAsync(
            releaseIntent.IntentId, "release-key", CancellationToken.None);

        Assert.Equal(ReleaseIntentOutcome.Released, release.Outcome);
        Assert.Equal(ReleaseIntentOutcome.ReleasedCached, releaseReplay.Outcome);
        Assert.Equal(TimeSpan.Zero, releaseReplay.ReleasedAt.Offset);
        AssertTimestampsMatch(release.ReleasedAt, releaseReplay.ReleasedAt);
        await AssertBalancesAsync(connectionString, (firstWalletId, 1_000, 0), (secondWalletId, 1_000, 0));
        await AssertLedgerAsync(connectionString, operationType: (short)OperationType.Release, expectedCount: 2);

        await using var connection = new NpgsqlConnection(connectionString);
        var invalidRelations = await connection.ExecuteScalarAsync<int>(
            "select count(*) from wallets.ledger_entries where relation_type is null or relation_type <> 0");
        Assert.Equal(0, invalidRelations);
    }

    private static async Task WaitForDatabaseAsync(string connectionString)
    {
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch (NpgsqlException) when (attempt < 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        await using var finalConnection = new NpgsqlConnection(connectionString);
        await finalConnection.OpenAsync();
    }

    private static async Task ResetAndMigrateAsync(string connectionString)
    {
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.ExecuteAsync(
                "drop schema if exists wallets cascade; drop table if exists public.\"__EFMigrationsHistory\";");
        }

        var options = new DbContextOptionsBuilder<WalletsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new WalletsDbContext(options);
        await context.Database.MigrateAsync();
    }

    private static async Task SeedWalletsAsync(string connectionString, params Guid[] walletIds)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        foreach (var walletId in walletIds)
        {
            await connection.ExecuteAsync(@"
insert into wallets.wallets
  (wallet_id, ""OwnerId"", wallet_type, status, currency, created_at, allowed_category_code, contract_expires_at)
values
  (@WalletId, @OwnerId, 2, 1, 'IRR', @Now, null, null);
insert into wallets.wallet_balances
  (wallet_id, available_minor, pending_minor, currency, last_ledger_entry_id, version, updated_at)
values
  (@WalletId, 1000, 0, 'IRR', null, 1, @Now);",
                new { WalletId = walletId, OwnerId = Guid.NewGuid(), Now = DateTimeOffset.UtcNow });
        }
    }

    private static async Task AssertBalancesAsync(
        string connectionString,
        params (Guid WalletId, long Available, long Pending)[] expected)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        foreach (var item in expected)
        {
            var balance = await connection.QuerySingleAsync<BalanceRow>(@"
select available_minor as Available, pending_minor as Pending
from wallets.wallet_balances where wallet_id = @WalletId;", new { item.WalletId });
            Assert.Equal(item.Available, balance.Available);
            Assert.Equal(item.Pending, balance.Pending);
        }
    }

    private static async Task AssertLedgerAsync(string connectionString, short operationType, int expectedCount)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        var count = await connection.ExecuteScalarAsync<int>(@"
select count(*) from wallets.ledger_entries
where operation_type = @OperationType and relation_type = 0 and related_entry_id is null;",
            new { OperationType = operationType });
        Assert.Equal(expectedCount, count);
    }

    private static void AssertTimestampsMatch(DateTimeOffset expected, DateTimeOffset actual)
    {
        var difference = (expected - actual).Duration();
        Assert.InRange(difference, TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
    }

    private sealed record BalanceRow(long Available, long Pending);
}
