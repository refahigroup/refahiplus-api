using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using Refahi.Modules.Wallets.Domain.Enums;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Context;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Refahi.Modules.Wallets.Tests;

public sealed class PaymentAtomicWriterPostgresTests
{
    private const string ConnectionVariable = "REFAHI_WALLETS_TEST_CONNECTION";

    [SkippableFact]
    public async Task EmptyDatabase_AllMigrationsAndWalletLifecycleSucceed()
    {
        var connectionString = RequireConnection();
        await WaitForDatabaseAsync(connectionString);
        await ResetAndMigrateAsync(connectionString);
        var repository = new WalletWriteRepository(connectionString);
        var payer = await repository.CreateAsync(Guid.NewGuid(), 1, 1, "IRR");
        var provider = await repository.CreateAsync(Guid.NewGuid(), 3, 1, "IRR");
        var topUpWriter = new WalletAtomicWriter(connectionString);
        await topUpWriter.ExecuteTopUpAsync(payer, "empty-db-topup", 1_000, "IRR", null, null, CancellationToken.None);
        var paymentWriter = new PaymentAtomicWriter(connectionString);
        var payment = await CreateCapturedPaymentAsync(paymentWriter, [new(payer, 600)], provider);
        await paymentWriter.ExecuteRefundPaymentAsync(payment.PaymentId, "empty-db-refund", null, null, CancellationToken.None, 100);
        var releasable = await paymentWriter.ExecuteCreateIntentAsync(Guid.NewGuid(), "empty-db-reserve", 200, "IRR", [new(payer, 200)], null, null, CancellationToken.None);
        await paymentWriter.ExecuteReleaseIntentAsync(releasable.IntentId, "empty-db-release", CancellationToken.None);
        var rebuilder = new BalanceRebuilder(connectionString, NullLogger<BalanceRebuilder>.Instance);
        var rebuilt = await rebuilder.RebuildSingleWalletAsync(payer, CancellationToken.None);

        Assert.False(rebuilt.Drift.HasDrift);
        await AssertNoDriftAsync(connectionString, payer, provider);
    }

    [SkippableFact]
    public async Task Migration_BackfillsHistoricalEffectsWithoutChangingBalances()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            Skip.If(true, $"PostgreSQL test skipped: {ConnectionVariable} is not configured.");

        var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains("test", connectionBuilder.Database, StringComparison.OrdinalIgnoreCase);
        await WaitForDatabaseAsync(connectionString);

        await using (var connection = new NpgsqlConnection(connectionString))
            await connection.ExecuteAsync(
                "drop schema if exists wallets cascade; drop table if exists public.\"__EFMigrationsHistory\";"
            );

        var options = new DbContextOptionsBuilder<WalletsDbContext>().UseNpgsql(connectionString).Options;
        await using (var context = new WalletsDbContext(options))
            await context.GetService<IMigrator>()
                .MigrateAsync("20260808120000_Wallets_SeedStoreSystemWallets");

        var ownerId = Guid.NewGuid();
        var userWalletId = Guid.NewGuid();
        var providerWalletId = Guid.NewGuid();
        var intentId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var topUpId = Guid.NewGuid();
        var holdId = Guid.NewGuid();
        var debitId = Guid.NewGuid();
        var providerCreditId = Guid.NewGuid();
        var intentAllocationId = Guid.NewGuid();
        var paymentAllocationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var connection = new NpgsqlConnection(connectionString))
            await connection.ExecuteAsync(
                """
                insert into wallets.wallets(wallet_id,"OwnerId",wallet_type,status,currency,created_at)
                values (@UserWalletId,@OwnerId,1,1,'IRR',@Now),
                       (@ProviderWalletId,@ProviderOwnerId,3,1,'IRR',@Now);
                insert into wallets.ledger_entries
                  (ledger_entry_id,wallet_id,operation_id,operation_type,entry_type,amount_minor,currency,effective_at,created_at,related_entry_id,relation_type)
                values
                  (@TopUpId,@UserWalletId,@TopUpId,1,1,1000,'IRR',@Now,@Now,null,0),
                  (@HoldId,@UserWalletId,@HoldId,2,3,400,'IRR',@Now,@Now,null,0),
                  (@DebitId,@UserWalletId,@DebitId,3,2,400,'IRR',@Now,@Now,null,0),
                  (@ProviderCreditId,@ProviderWalletId,@ProviderCreditId,3,1,400,'IRR',@Now,@Now,null,0);
                insert into wallets.wallet_balances(wallet_id,available_minor,pending_minor,currency,last_ledger_entry_id,version,updated_at)
                values (@UserWalletId,600,0,'IRR',@DebitId,3,@Now),
                       (@ProviderWalletId,400,0,'IRR',@ProviderCreditId,1,@Now);
                insert into wallets.payment_intents(intent_id,order_id,idempotency_key,status,amount_minor,currency,destination_wallet_id,created_at,captured_at)
                values (@IntentId,@OrderId,'historical-reserve',2,400,'IRR',@ProviderWalletId,@Now,@Now);
                insert into wallets.payment_intent_allocations(allocation_id,intent_id,wallet_id,amount_minor,sequence)
                values (@IntentAllocationId,@IntentId,@UserWalletId,400,0);
                insert into wallets.payments(payment_id,intent_id,order_id,status,amount_minor,currency,destination_wallet_id,destination_ledger_entry_id,completed_at)
                values (@PaymentId,@IntentId,@OrderId,1,400,'IRR',@ProviderWalletId,@ProviderCreditId,@Now);
                insert into wallets.payment_allocations(allocation_id,payment_id,wallet_id,amount_minor,sequence,ledger_entry_id)
                values (@PaymentAllocationId,@PaymentId,@UserWalletId,400,0,@DebitId);
                """,
                new
                {
                    UserWalletId = userWalletId,
                    ProviderWalletId = providerWalletId,
                    OwnerId = ownerId,
                    ProviderOwnerId = Guid.NewGuid(),
                    IntentId = intentId,
                    PaymentId = paymentId,
                    OrderId = orderId,
                    TopUpId = topUpId,
                    HoldId = holdId,
                    DebitId = debitId,
                    ProviderCreditId = providerCreditId,
                    IntentAllocationId = intentAllocationId,
                    PaymentAllocationId = paymentAllocationId,
                    Now = now,
                }
            );

        await using (var context = new WalletsDbContext(options))
            await context.Database.MigrateAsync();

        await AssertBalancesAsync(
            connectionString,
            (userWalletId, 600, 0),
            (providerWalletId, 400, 0)
        );
        await AssertNoDriftAsync(connectionString, userWalletId, providerWalletId);

        var writer = new PaymentAtomicWriter(connectionString);
        var historicalReplay = await writer.ExecuteCreateIntentAsync(
            orderId, "historical-reserve", 400, "IRR", [new(userWalletId, 400)],
            null, providerWalletId, CancellationToken.None);
        Assert.Equal(CreateIntentOutcome.CreatedCached, historicalReplay.Outcome);
        await Assert.ThrowsAsync<Refahi.Modules.Wallets.Application.Contracts.Exceptions.IdempotencyKeyConflictException>(
            () => writer.ExecuteCreateIntentAsync(
                orderId, "historical-reserve", 400, "IRR", [new(userWalletId, 400)],
                null, providerWalletId, CancellationToken.None,
                payerOwnerId: ownerId, categoryCodes: ["store"]));
    }

    [SkippableFact]
    public async Task Migration_BackfillsRepresentativePreviousSchemaHistory()
    {
        var connectionString = RequireConnection();
        await WaitForDatabaseAsync(connectionString);
        await ResetToMigrationAsync(connectionString, "20260808120000_Wallets_SeedStoreSystemWallets");
        await using (var connection = new NpgsqlConnection(connectionString))
            await connection.ExecuteAsync(
                """
                insert into wallets.wallets(wallet_id,"OwnerId",wallet_type,status,currency,created_at,allowed_category_code,contract_expires_at) values
                ('00000000-0000-0000-0000-000000000101',gen_random_uuid(),1,1,'IRR',now(),null,null),
                ('00000000-0000-0000-0000-000000000102',gen_random_uuid(),2,1,'IRR',now(),'store',now()+interval '1 year'),
                ('00000000-0000-0000-0000-000000000103',gen_random_uuid(),3,1,'IRR',now(),null,null),
                ('00000000-0000-0000-0000-000000000104',gen_random_uuid(),1,1,'IRR',now(),null,null);

                insert into wallets.ledger_entries(ledger_entry_id,wallet_id,operation_id,operation_type,entry_type,amount_minor,currency,effective_at,created_at,related_entry_id,relation_type,external_reference) values
                ('00000000-0000-0000-0000-000000001001','00000000-0000-0000-0000-000000000101',gen_random_uuid(),1,1,1000,'IRR',now(),now(),null,0,'topup:user'),
                ('00000000-0000-0000-0000-000000001002','00000000-0000-0000-0000-000000000102',gen_random_uuid(),1,1,1000,'IRR',now(),now(),null,0,'topup:org'),
                ('00000000-0000-0000-0000-000000001003','00000000-0000-0000-0000-000000000104',gen_random_uuid(),1,1,500,'IRR',now(),now(),null,0,'topup:system'),
                ('00000000-0000-0000-0000-000000001011','00000000-0000-0000-0000-000000000101',gen_random_uuid(),2,3,200,'IRR',now(),now(),null,0,'intent:00000000-0000-0000-0000-000000000201'),
                ('00000000-0000-0000-0000-000000001012','00000000-0000-0000-0000-000000000102',gen_random_uuid(),2,3,200,'IRR',now(),now(),null,0,'intent:00000000-0000-0000-0000-000000000201'),
                ('00000000-0000-0000-0000-000000001013','00000000-0000-0000-0000-000000000101',gen_random_uuid(),3,2,200,'IRR',now(),now(),null,0,'payment:1'),
                ('00000000-0000-0000-0000-000000001014','00000000-0000-0000-0000-000000000102',gen_random_uuid(),3,2,200,'IRR',now(),now(),null,0,'payment:1'),
                ('00000000-0000-0000-0000-000000001015','00000000-0000-0000-0000-000000000103',gen_random_uuid(),3,1,400,'IRR',now(),now(),null,0,'payment:1'),
                ('00000000-0000-0000-0000-000000001021','00000000-0000-0000-0000-000000000101',gen_random_uuid(),2,3,100,'IRR',now(),now(),null,0,'intent:00000000-0000-0000-0000-000000000202'),
                ('00000000-0000-0000-0000-000000001022','00000000-0000-0000-0000-000000000101',gen_random_uuid(),3,2,100,'IRR',now(),now(),null,0,'payment:2'),
                ('00000000-0000-0000-0000-000000001023','00000000-0000-0000-0000-000000000103',gen_random_uuid(),3,1,150,'IRR',now(),now(),null,0,'payment:2'),
                ('00000000-0000-0000-0000-000000001024','00000000-0000-0000-0000-000000000104',gen_random_uuid(),3,2,50,'IRR',now(),now(),null,0,'payment:2'),
                ('00000000-0000-0000-0000-000000001025','00000000-0000-0000-0000-000000000101',gen_random_uuid(),5,1,100,'IRR',now(),now(),null,0,'refund:2'),
                ('00000000-0000-0000-0000-000000001026','00000000-0000-0000-0000-000000000103',gen_random_uuid(),5,2,150,'IRR',now(),now(),null,0,'refund:2'),
                ('00000000-0000-0000-0000-000000001027','00000000-0000-0000-0000-000000000104',gen_random_uuid(),5,1,50,'IRR',now(),now(),null,0,'refund:2'),
                ('00000000-0000-0000-0000-000000001031','00000000-0000-0000-0000-000000000101',gen_random_uuid(),2,3,50,'IRR',now(),now(),null,0,'intent:00000000-0000-0000-0000-000000000203'),
                ('00000000-0000-0000-0000-000000001032','00000000-0000-0000-0000-000000000101',gen_random_uuid(),4,4,50,'IRR',now(),now(),null,0,'release|intent:00000000-0000-0000-0000-000000000203');

                insert into wallets.wallet_balances(wallet_id,available_minor,pending_minor,currency,last_ledger_entry_id,version,updated_at) values
                ('00000000-0000-0000-0000-000000000101',800,0,'IRR','00000000-0000-0000-0000-000000001032',8,now()),
                ('00000000-0000-0000-0000-000000000102',800,0,'IRR','00000000-0000-0000-0000-000000001014',3,now()),
                ('00000000-0000-0000-0000-000000000103',400,0,'IRR','00000000-0000-0000-0000-000000001026',4,now()),
                ('00000000-0000-0000-0000-000000000104',500,0,'IRR','00000000-0000-0000-0000-000000001027',3,now());

                insert into wallets.payment_intents(intent_id,order_id,idempotency_key,status,amount_minor,currency,destination_wallet_id,created_at,captured_at,released_at) values
                ('00000000-0000-0000-0000-000000000201',gen_random_uuid(),'history-multi',2,400,'IRR','00000000-0000-0000-0000-000000000103',now(),now(),null),
                ('00000000-0000-0000-0000-000000000202',gen_random_uuid(),'history-posting',2,100,'IRR',null,now(),now(),null),
                ('00000000-0000-0000-0000-000000000203',gen_random_uuid(),'history-release',3,50,'IRR',null,now(),null,now());
                insert into wallets.payment_intent_allocations(allocation_id,intent_id,wallet_id,amount_minor,sequence) values
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000201','00000000-0000-0000-0000-000000000101',200,0),
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000201','00000000-0000-0000-0000-000000000102',200,1),
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000202','00000000-0000-0000-0000-000000000101',100,0),
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000203','00000000-0000-0000-0000-000000000101',50,0);
                insert into wallets.payments(payment_id,intent_id,order_id,status,amount_minor,currency,destination_wallet_id,destination_ledger_entry_id,completed_at) values
                ('00000000-0000-0000-0000-000000000301','00000000-0000-0000-0000-000000000201',gen_random_uuid(),1,400,'IRR','00000000-0000-0000-0000-000000000103','00000000-0000-0000-0000-000000001015',now()),
                ('00000000-0000-0000-0000-000000000302','00000000-0000-0000-0000-000000000202',gen_random_uuid(),1,100,'IRR',null,null,now());
                insert into wallets.payment_allocations(allocation_id,payment_id,wallet_id,amount_minor,sequence,ledger_entry_id) values
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000301','00000000-0000-0000-0000-000000000101',200,0,'00000000-0000-0000-0000-000000001013'),
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000301','00000000-0000-0000-0000-000000000102',200,1,'00000000-0000-0000-0000-000000001014'),
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000302','00000000-0000-0000-0000-000000000101',100,0,'00000000-0000-0000-0000-000000001022');
                insert into wallets.payment_intent_postings(posting_id,intent_id,wallet_id,direction,amount_minor,purpose,sequence) values
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000202','00000000-0000-0000-0000-000000000103',1,150,'provider',0),
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000202','00000000-0000-0000-0000-000000000104',2,50,'commission',1);
                insert into wallets.payment_postings(posting_id,payment_id,wallet_id,direction,amount_minor,purpose,sequence,ledger_entry_id) values
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000302','00000000-0000-0000-0000-000000000103',1,150,'provider',0,'00000000-0000-0000-0000-000000001023'),
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000302','00000000-0000-0000-0000-000000000104',2,50,'commission',1,'00000000-0000-0000-0000-000000001024');
                insert into wallets.refunds(refund_id,payment_id,order_id,status,amount_minor,currency,reason,created_at,completed_at) values
                ('00000000-0000-0000-0000-000000000401','00000000-0000-0000-0000-000000000302',gen_random_uuid(),1,100,'IRR','historical',now(),now());
                insert into wallets.refund_allocations(allocation_id,refund_id,wallet_id,amount_minor,sequence,ledger_entry_id) values
                (gen_random_uuid(),'00000000-0000-0000-0000-000000000401','00000000-0000-0000-0000-000000000101',100,0,'00000000-0000-0000-0000-000000001025');
                """);

        var options = new DbContextOptionsBuilder<WalletsDbContext>().UseNpgsql(connectionString).Options;
        await using (var context = new WalletsDbContext(options))
            await context.Database.MigrateAsync();

        var wallets = new[] {
            Guid.Parse("00000000-0000-0000-0000-000000000101"),
            Guid.Parse("00000000-0000-0000-0000-000000000102"),
            Guid.Parse("00000000-0000-0000-0000-000000000103"),
            Guid.Parse("00000000-0000-0000-0000-000000000104") };
        await AssertBalancesAsync(connectionString, (wallets[0],800,0), (wallets[1],800,0), (wallets[2],400,0), (wallets[3],500,0));
        await AssertNoDriftAsync(connectionString, wallets);
    }

    [SkippableFact]
    public async Task PaymentLifecycle_IsAtomicIdempotentAndPersistsRelationType()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            Skip.If(true, $"PostgreSQL test skipped: {ConnectionVariable} is not configured.");

        var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains("test", connectionBuilder.Database, StringComparison.OrdinalIgnoreCase);

        await WaitForDatabaseAsync(connectionString);
        await ResetAndMigrateAsync(connectionString);

        var firstWalletId = Guid.NewGuid();
        var secondWalletId = Guid.NewGuid();
        var providerWalletId = Guid.NewGuid();
        await SeedWalletsAsync(connectionString, firstWalletId, secondWalletId);
        await SeedProviderWalletAsync(connectionString, providerWalletId);

        var writer = new PaymentAtomicWriter(connectionString);
        var allocations = new List<AllocationInput>
        {
            new(firstWalletId, 600),
            new(secondWalletId, 400),
        };

        var orderId = Guid.NewGuid();
        const string metadataJson = "{\"source\":\"integration-test\"}";
        var reserve = await writer.ExecuteCreateIntentAsync(
            orderId,
            "reserve-key",
            1_000,
            "IRR",
            allocations,
            metadataJson,
            providerWalletId,
            CancellationToken.None
        );
        var reserveReplay = await writer.ExecuteCreateIntentAsync(
            orderId,
            "reserve-key",
            1_000,
            "IRR",
            allocations,
            metadataJson,
            providerWalletId,
            CancellationToken.None
        );

        Assert.Equal(CreateIntentOutcome.Created, reserve.Outcome);
        Assert.Equal(CreateIntentOutcome.CreatedCached, reserveReplay.Outcome);
        await Assert.ThrowsAsync<Refahi.Modules.Wallets.Application.Contracts.Exceptions.IdempotencyKeyConflictException>(
            () => writer.ExecuteCreateIntentAsync(
                orderId,
                "reserve-key",
                999,
                "IRR",
                [new(firstWalletId, 599), new(secondWalletId, 400)],
                metadataJson,
                providerWalletId,
                CancellationToken.None
            )
        );
        Assert.Equal(TimeSpan.Zero, reserveReplay.CreatedAt.Offset);
        AssertTimestampsMatch(reserve.CreatedAt, reserveReplay.CreatedAt);
        await AssertBalancesAsync(
            connectionString,
            (firstWalletId, 400, 600),
            (secondWalletId, 600, 400)
        );
        await AssertLedgerAsync(
            connectionString,
            operationType: (short)OperationType.Reserve,
            expectedCount: 2
        );

        var capture = await writer.ExecuteCaptureIntentAsync(
            reserve.IntentId,
            "capture-key",
            CancellationToken.None
        );
        var captureReplay = await writer.ExecuteCaptureIntentAsync(
            reserve.IntentId,
            "capture-key",
            CancellationToken.None
        );

        Assert.Equal(CaptureIntentOutcome.Captured, capture.Outcome);
        Assert.Equal(CaptureIntentOutcome.CapturedCached, captureReplay.Outcome);
        Assert.Equal(capture.PaymentId, captureReplay.PaymentId);
        Assert.Equal(TimeSpan.Zero, captureReplay.CompletedAt.Offset);
        AssertTimestampsMatch(capture.CompletedAt, captureReplay.CompletedAt);
        await AssertNoDriftAsync(connectionString, firstWalletId, secondWalletId, providerWalletId);

        await AssertBalancesAsync(
            connectionString,
            (firstWalletId, 400, 0),
            (secondWalletId, 600, 0)
        );
        await AssertBalancesAsync(connectionString, (providerWalletId, 1_000, 0));
        await AssertLedgerAsync(
            connectionString,
            operationType: (short)OperationType.Payment,
            expectedCount: 3
        );

        var refund = await writer.ExecuteRefundPaymentAsync(
            capture.PaymentId,
            "refund-key",
            "integration test",
            null,
            CancellationToken.None
        );
        var refundReplay = await writer.ExecuteRefundPaymentAsync(
            capture.PaymentId,
            "refund-key",
            "integration test",
            null,
            CancellationToken.None
        );

        Assert.Equal(RefundPaymentOutcome.Refunded, refund.Outcome);
        Assert.Equal(RefundPaymentOutcome.RefundedCached, refundReplay.Outcome);
        await Assert.ThrowsAsync<Refahi.Modules.Wallets.Application.Contracts.Exceptions.IdempotencyKeyConflictException>(
            () => writer.ExecuteRefundPaymentAsync(
                capture.PaymentId,
                "refund-key",
                "different reason",
                null,
                CancellationToken.None
            )
        );
        Assert.Equal(refund.RefundId, refundReplay.RefundId);
        Assert.Equal(TimeSpan.Zero, refundReplay.CompletedAt.Offset);
        AssertTimestampsMatch(refund.CompletedAt, refundReplay.CompletedAt);
        await AssertBalancesAsync(
            connectionString,
            (firstWalletId, 1_000, 0),
            (secondWalletId, 1_000, 0)
        );
        await AssertBalancesAsync(connectionString, (providerWalletId, 0, 0));
        await AssertLedgerAsync(
            connectionString,
            operationType: (short)OperationType.Refund,
            expectedCount: 3,
            relationType: (short)RelationType.Reversal
        );
        await AssertNoDriftAsync(connectionString, firstWalletId, secondWalletId, providerWalletId);

        var releaseIntent = await writer.ExecuteCreateIntentAsync(
            Guid.NewGuid(),
            "release-reserve-key",
            300,
            "IRR",
            [new(firstWalletId, 100), new(secondWalletId, 200)],
            null,
            null,
            CancellationToken.None
        );
        var release = await writer.ExecuteReleaseIntentAsync(
            releaseIntent.IntentId,
            "release-key",
            CancellationToken.None
        );
        var releaseReplay = await writer.ExecuteReleaseIntentAsync(
            releaseIntent.IntentId,
            "release-key",
            CancellationToken.None
        );

        Assert.Equal(ReleaseIntentOutcome.Released, release.Outcome);
        Assert.Equal(ReleaseIntentOutcome.ReleasedCached, releaseReplay.Outcome);
        Assert.Equal(TimeSpan.Zero, releaseReplay.ReleasedAt.Offset);
        AssertTimestampsMatch(release.ReleasedAt, releaseReplay.ReleasedAt);
        await AssertBalancesAsync(
            connectionString,
            (firstWalletId, 1_000, 0),
            (secondWalletId, 1_000, 0)
        );
        await AssertLedgerAsync(
            connectionString,
            operationType: (short)OperationType.Release,
            expectedCount: 2,
            relationType: (short)RelationType.Reversal
        );
        await AssertNoDriftAsync(connectionString, firstWalletId, secondWalletId, providerWalletId);

        var racingIntent = await writer.ExecuteCreateIntentAsync(
            Guid.NewGuid(),
            "race-reserve-key",
            100,
            "IRR",
            [new(firstWalletId, 100)],
            null,
            null,
            CancellationToken.None
        );
        await Task.WhenAll(
            ObserveTerminalRaceAsync(() => writer.ExecuteCaptureIntentAsync(
                racingIntent.IntentId,
                "race-capture-key",
                CancellationToken.None)),
            ObserveTerminalRaceAsync(() => writer.ExecuteReleaseIntentAsync(
                racingIntent.IntentId,
                "race-release-key",
                CancellationToken.None))
        );
        await AssertNoDriftAsync(connectionString, firstWalletId);

        await using var connection = new NpgsqlConnection(connectionString);
        var invalidRelations = await connection.ExecuteScalarAsync<int>(
            @"
select count(*) from wallets.ledger_entries
where relation_type is null
   or (relation_type = 0 and related_entry_id is not null)
   or (relation_type = 1 and related_entry_id is null)
   or relation_type not in (0, 1)"
        );
        Assert.Equal(0, invalidRelations);
    }

    [SkippableFact]
    public async Task PartialAndMultipleRefunds_PreserveExactAllocationsAndLedgerProjection()
    {
        var connectionString = RequireConnection();
        await ResetAndMigrateAsync(connectionString);
        var orgWallet = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var userWallet = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var providerWallet = Guid.NewGuid();
        await SeedWalletAsync(connectionString, orgWallet, 10_000, walletType: 2);
        await SeedWalletAsync(connectionString, userWallet, 10_000, walletType: 1);
        await SeedProviderWalletAsync(connectionString, providerWallet);

        var writer = new PaymentAtomicWriter(connectionString);
        var payment = await CreateCapturedPaymentAsync(
            writer,
            [new(orgWallet, 6_000), new(userWallet, 4_000)],
            providerWallet
        );

        var first = await writer.ExecuteRefundPaymentAsync(payment.PaymentId, "partial-1", "اول", null, CancellationToken.None, 2_000);
        var second = await writer.ExecuteRefundPaymentAsync(payment.PaymentId, "partial-2", "دوم", null, CancellationToken.None, 3_000);
        var third = await writer.ExecuteRefundPaymentAsync(payment.PaymentId, "partial-3", "سوم", null, CancellationToken.None, 5_000);

        Assert.Equal(2_000, first.AmountMinor);
        Assert.Equal(3_000, second.AmountMinor);
        Assert.Equal(5_000, third.AmountMinor);
        await using var connection = new NpgsqlConnection(connectionString);
        var totals = (await connection.QueryAsync<(Guid WalletId, long Amount)>(
            """
            select ra.wallet_id as WalletId, sum(ra.amount_minor)::bigint as Amount
            from wallets.refund_allocations ra
            join wallets.refunds r on r.refund_id=ra.refund_id
            where r.payment_id=@PaymentId and r.status=1
            group by ra.wallet_id
            """, new { payment.PaymentId })).ToDictionary(x => x.WalletId, x => x.Amount);
        Assert.Equal(6_000, totals[orgWallet]);
        Assert.Equal(4_000, totals[userWallet]);
        await AssertBalancesAsync(connectionString, (orgWallet, 10_000, 0), (userWallet, 10_000, 0), (providerWallet, 0, 0));
        await AssertNoDriftAsync(connectionString, orgWallet, userWallet, providerWallet);
    }

    [SkippableFact]
    public async Task PartialRefund_RejectsOverRefundAndAcceptsExactRemainingAmount()
    {
        var connectionString = RequireConnection();
        await ResetAndMigrateAsync(connectionString);
        var payer = Guid.NewGuid();
        var provider = Guid.NewGuid();
        await SeedWalletAsync(connectionString, payer, 10_000, 1);
        await SeedProviderWalletAsync(connectionString, provider);
        var writer = new PaymentAtomicWriter(connectionString);
        var payment = await CreateCapturedPaymentAsync(writer, [new(payer, 10_000)], provider);

        await writer.ExecuteRefundPaymentAsync(payment.PaymentId, "refund-8000", null, null, CancellationToken.None, 8_000);
        await Assert.ThrowsAsync<Refahi.Modules.Wallets.Application.Contracts.Exceptions.PaymentNotRefundableException>(
            () => writer.ExecuteRefundPaymentAsync(payment.PaymentId, "refund-over", null, null, CancellationToken.None, 2_001));
        var exact = await writer.ExecuteRefundPaymentAsync(payment.PaymentId, "refund-exact", null, null, CancellationToken.None, 2_000);

        Assert.Equal(2_000, exact.AmountMinor);
        await AssertBalancesAsync(connectionString, (payer, 10_000, 0), (provider, 0, 0));
        await AssertNoDriftAsync(connectionString, payer, provider);
    }

    [SkippableFact]
    public async Task PartialRefund_ReversesMultiplePostingsProportionally()
    {
        var connectionString = RequireConnection();
        await ResetAndMigrateAsync(connectionString);
        var payer = Guid.NewGuid();
        var creditWallet = Guid.NewGuid();
        var debitWallet = Guid.NewGuid();
        await SeedWalletAsync(connectionString, payer, 10_000, 1);
        await SeedWalletAsync(connectionString, creditWallet, 0, 3);
        await SeedWalletAsync(connectionString, debitWallet, 1_000, 3);
        var writer = new PaymentAtomicWriter(connectionString);
        var payment = await CreateCapturedPaymentAsync(
            writer,
            [new(payer, 1_000)],
            postings: [new(creditWallet, 1, 1_200, "provider"), new(debitWallet, 2, 200, "commission")]
        );

        await writer.ExecuteRefundPaymentAsync(payment.PaymentId, "posting-partial", null, null, CancellationToken.None, 500);

        await AssertBalancesAsync(connectionString, (payer, 9_500, 0), (creditWallet, 600, 0), (debitWallet, 900, 0));
        await AssertNoDriftAsync(connectionString, payer, creditWallet, debitWallet);
    }

    [SkippableFact]
    public async Task ConcurrentRefunds_AreIdempotentAndCannotOverRefund()
    {
        var connectionString = RequireConnection();
        await ResetAndMigrateAsync(connectionString);
        var payer = Guid.NewGuid();
        var provider = Guid.NewGuid();
        await SeedWalletAsync(connectionString, payer, 10_000, 1);
        await SeedProviderWalletAsync(connectionString, provider);
        var writer = new PaymentAtomicWriter(connectionString);
        var payment = await CreateCapturedPaymentAsync(writer, [new(payer, 10_000)], provider);

        var duplicates = await Task.WhenAll(
            writer.ExecuteRefundPaymentAsync(payment.PaymentId, "same-refund", null, null, CancellationToken.None, 2_000),
            writer.ExecuteRefundPaymentAsync(payment.PaymentId, "same-refund", null, null, CancellationToken.None, 2_000));
        Assert.Single(duplicates.Select(x => x.RefundId).Distinct());

        await writer.ExecuteRefundPaymentAsync(payment.PaymentId, "to-eight", null, null, CancellationToken.None, 6_000);
        await Task.WhenAll(
            ObserveRefundRaceAsync(() => writer.ExecuteRefundPaymentAsync(payment.PaymentId, "limit-a", null, null, CancellationToken.None, 2_000)),
            ObserveRefundRaceAsync(() => writer.ExecuteRefundPaymentAsync(payment.PaymentId, "limit-b", null, null, CancellationToken.None, 2_000)));

        await using var connection = new NpgsqlConnection(connectionString);
        var total = await connection.ExecuteScalarAsync<long>(
            "select coalesce(sum(amount_minor),0)::bigint from wallets.refunds where payment_id=@PaymentId and status=1",
            new { payment.PaymentId });
        Assert.Equal(10_000, total);
        await AssertBalancesAsync(connectionString, (payer, 10_000, 0), (provider, 0, 0));
        await AssertNoDriftAsync(connectionString, payer, provider);
    }

    [SkippableFact]
    public async Task ConcurrentFullAndPartialRefund_NeverExceedsCapturedAmount()
    {
        var connectionString = RequireConnection();
        await ResetAndMigrateAsync(connectionString);
        var payer = Guid.NewGuid();
        var provider = Guid.NewGuid();
        await SeedWalletAsync(connectionString, payer, 1_000, 1);
        await SeedProviderWalletAsync(connectionString, provider);
        var writer = new PaymentAtomicWriter(connectionString);
        var payment = await CreateCapturedPaymentAsync(writer, [new(payer, 1_000)], provider);

        await Task.WhenAll(
            ObserveRefundRaceAsync(() => writer.ExecuteRefundPaymentAsync(payment.PaymentId, "full", null, null, CancellationToken.None)),
            ObserveRefundRaceAsync(() => writer.ExecuteRefundPaymentAsync(payment.PaymentId, "partial", null, null, CancellationToken.None, 200)));

        await using var connection = new NpgsqlConnection(connectionString);
        var total = await connection.ExecuteScalarAsync<long>(
            "select coalesce(sum(amount_minor),0)::bigint from wallets.refunds where payment_id=@PaymentId and status=1",
            new { payment.PaymentId });
        Assert.InRange(total, 200, 1_000);
        await AssertNoDriftAsync(connectionString, payer, provider);
    }

    [SkippableFact]
    public async Task CanonicalIntentHash_DetectsMaterialChangesAndAcceptsEquivalentOrder()
    {
        var connectionString = RequireConnection();
        await ResetAndMigrateAsync(connectionString);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var payer = Guid.NewGuid();
        await SeedWalletAsync(connectionString, first, 1_000, 1, payer);
        await SeedWalletAsync(connectionString, second, 1_000, 1, payer);
        var writer = new PaymentAtomicWriter(connectionString);
        var order = Guid.NewGuid();
        var created = await writer.ExecuteCreateIntentAsync(order, "canonical", 1_000, "IRR",
            [new(first, 600), new(second, 400)], "{\"b\":2,\"a\":1}", null,
            CancellationToken.None, payerOwnerId: payer, categoryCodes: ["Store", "store.clothing"]);
        var replay = await writer.ExecuteCreateIntentAsync(order, "canonical", 1_000, "IRR",
            [new(second, 400), new(first, 600)], "{\"a\":1,\"b\":2}", null,
            CancellationToken.None, payerOwnerId: payer, categoryCodes: ["store.clothing", "store"]);
        Assert.Equal(CreateIntentOutcome.CreatedCached, replay.Outcome);
        Assert.Equal(created.IntentId, replay.IntentId);
        await Assert.ThrowsAsync<Refahi.Modules.Wallets.Application.Contracts.Exceptions.IdempotencyKeyConflictException>(
            () => writer.ExecuteCreateIntentAsync(order, "canonical", 1_000, "IRR",
                [new(first, 600), new(second, 400)], "{\"a\":1,\"b\":2}", null,
                CancellationToken.None, payerOwnerId: Guid.NewGuid(), categoryCodes: ["store", "store.clothing"]));
    }

    [SkippableFact]
    public async Task ConcurrentReserveCaptureAndRelease_AreDeterministicWithoutOverdrawOrDeadlock()
    {
        var connectionString = RequireConnection();
        await ResetAndMigrateAsync(connectionString);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await SeedWalletAsync(connectionString, first, 1_000, 1);
        await SeedWalletAsync(connectionString, second, 1_000, 1);
        var writer = new PaymentAtomicWriter(connectionString);

        await Task.WhenAll(
            ObserveReserveRaceAsync(() => writer.ExecuteCreateIntentAsync(Guid.NewGuid(), "same-wallet-a", 800, "IRR", [new(first, 800)], null, null, CancellationToken.None)),
            ObserveReserveRaceAsync(() => writer.ExecuteCreateIntentAsync(Guid.NewGuid(), "same-wallet-b", 800, "IRR", [new(first, 800)], null, null, CancellationToken.None)));
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            var balance = await connection.ExecuteScalarAsync<long>("select available_minor from wallets.wallet_balances where wallet_id=@first", new { first });
            Assert.InRange(balance, 200, 1_000);
        }

        await ResetAndMigrateAsync(connectionString);
        await SeedWalletAsync(connectionString, first, 1_000, 1);
        await SeedWalletAsync(connectionString, second, 1_000, 1);
        writer = new PaymentAtomicWriter(connectionString);
        await Task.WhenAll(
            ObserveReserveRaceAsync(() => writer.ExecuteCreateIntentAsync(Guid.NewGuid(), "multi-a", 1_100, "IRR", [new(first, 700), new(second, 400)], null, null, CancellationToken.None)),
            ObserveReserveRaceAsync(() => writer.ExecuteCreateIntentAsync(Guid.NewGuid(), "multi-b", 1_100, "IRR", [new(second, 700), new(first, 400)], null, null, CancellationToken.None)));
        await AssertNoDriftAsync(connectionString, first, second);

        await ResetAndMigrateAsync(connectionString);
        await SeedWalletAsync(connectionString, first, 1_000, 1);
        writer = new PaymentAtomicWriter(connectionString);
        var captureIntent = await writer.ExecuteCreateIntentAsync(Guid.NewGuid(), "capture-reserve", 500, "IRR", [new(first, 500)], null, null, CancellationToken.None);
        await Task.WhenAll(
            ObserveTerminalRaceAsync(() => writer.ExecuteCaptureIntentAsync(captureIntent.IntentId, "capture-a", CancellationToken.None)),
            ObserveTerminalRaceAsync(() => writer.ExecuteCaptureIntentAsync(captureIntent.IntentId, "capture-b", CancellationToken.None)));
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            var payments = await connection.ExecuteScalarAsync<int>("select count(*) from wallets.payments where intent_id=@IntentId", new { captureIntent.IntentId });
            Assert.Equal(1, payments);
        }

        var releaseIntent = await writer.ExecuteCreateIntentAsync(Guid.NewGuid(), "release-reserve", 200, "IRR", [new(first, 200)], null, null, CancellationToken.None);
        await Task.WhenAll(
            ObserveTerminalRaceAsync(() => writer.ExecuteReleaseIntentAsync(releaseIntent.IntentId, "release-a", CancellationToken.None)),
            ObserveTerminalRaceAsync(() => writer.ExecuteReleaseIntentAsync(releaseIntent.IntentId, "release-b", CancellationToken.None)));
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            var releases = await connection.ExecuteScalarAsync<int>("select count(*) from wallets.ledger_entries where operation_type=4 and external_reference like @Pattern", new { Pattern = $"%intent:{releaseIntent.IntentId}" });
            Assert.Equal(1, releases);
        }
        await AssertNoDriftAsync(connectionString, first);
    }

    [SkippableFact]
    public async Task AmbiguousHistoricalMigration_FailsTransactionally()
    {
        var connectionString = RequireConnection();
        await ResetToMigrationAsync(connectionString, "20260808120000_Wallets_SeedStoreSystemWallets");
        var wallet = Guid.NewGuid();
        var ledger = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(connectionString))
            await connection.ExecuteAsync(
                """
                insert into wallets.wallets(wallet_id,"OwnerId",wallet_type,status,currency,created_at)
                values (@Wallet,@Owner,1,1,'IRR',@Now);
                insert into wallets.ledger_entries(ledger_entry_id,wallet_id,operation_id,operation_type,entry_type,amount_minor,currency,effective_at,created_at,relation_type)
                values (@Ledger,@Wallet,@Ledger,3,2,100,'IRR',@Now,@Now,0);
                insert into wallets.wallet_balances(wallet_id,available_minor,pending_minor,currency,version,updated_at)
                values (@Wallet,0,0,'IRR',1,@Now);
                """, new { Wallet = wallet, Owner = Guid.NewGuid(), Ledger = ledger, Now = DateTimeOffset.UtcNow });

        var options = new DbContextOptionsBuilder<WalletsDbContext>().UseNpgsql(connectionString).Options;
        await using (var context = new WalletsDbContext(options))
            await Assert.ThrowsAnyAsync<Exception>(() => context.Database.MigrateAsync());

        await using var verify = new NpgsqlConnection(connectionString);
        var deltaColumnCount = await verify.ExecuteScalarAsync<int>(
            "select count(*) from information_schema.columns where table_schema='wallets' and table_name='ledger_entries' and column_name='available_delta_minor'");
        Assert.Equal(0, deltaColumnCount);
    }

    [SkippableFact]
    public async Task StructuralFinancialInvariants_AreDatabaseEnforced()
    {
        var connectionString = RequireConnection();
        await ResetAndMigrateAsync(connectionString);
        var wallet = Guid.NewGuid();
        await SeedWalletAsync(connectionString, wallet, 100, 1);
        await using var connection = new NpgsqlConnection(connectionString);
        var anomalies = await connection.ExecuteScalarAsync<int>(
            """
            select
              (select count(*) from wallets.ledger_entries le left join wallets.wallets w on w.wallet_id=le.wallet_id where w.wallet_id is null)
              + (select count(*) from wallets.ledger_entries le left join wallets.wallet_balances b on b.wallet_id=le.wallet_id where b.wallet_id is null)
              + (select count(*) from wallets.wallet_balances b left join wallets.wallets w on w.wallet_id=b.wallet_id where w.wallet_id is null)
              + (select count(*) from wallets.wallets w left join wallets.wallet_balances b on b.wallet_id=w.wallet_id where w.status=1 and b.wallet_id is null)
            """);
        Assert.Equal(0, anomalies);

        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "insert into wallets.ledger_entries(ledger_entry_id,wallet_id,operation_id,operation_type,entry_type,amount_minor,available_delta_minor,pending_delta_minor,currency,effective_at,created_at,relation_type) values (@Id,@Missing,@Id,1,1,1,1,0,'IRR',now(),now(),0)",
            new { Id = Guid.NewGuid(), Missing = Guid.NewGuid() }));
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "insert into wallets.wallet_balances(wallet_id,available_minor,pending_minor,currency,version,updated_at) values (@Wallet,0,0,'IRR',1,now())",
            new { Wallet = wallet }));
    }

    [SkippableFact]
    public async Task Pass2Migration_CanRollBackBeforeMultipleRefundDataExists()
    {
        var connectionString = RequireConnection();
        await ResetAndMigrateAsync(connectionString);
        var options = new DbContextOptionsBuilder<WalletsDbContext>().UseNpgsql(connectionString).Options;
        await using (var context = new WalletsDbContext(options))
            await context.GetService<IMigrator>().MigrateAsync("20260915191531_Wallets_AddExplicitLedgerBalanceEffects");
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            var pass2Columns = await connection.ExecuteScalarAsync<int>(
                "select count(*) from information_schema.columns where table_schema='wallets' and column_name='request_hash_version'");
            Assert.Equal(0, pass2Columns);
        }
        await using (var context = new WalletsDbContext(options))
            await context.Database.MigrateAsync();
    }

    private static string RequireConnection()
    {
        var value = Environment.GetEnvironmentVariable(ConnectionVariable);
        Skip.If(string.IsNullOrWhiteSpace(value), $"PostgreSQL test skipped: {ConnectionVariable} is not configured.");
        return value!;
    }

    private static async Task<CaptureIntentAtomicResult> CreateCapturedPaymentAsync(
        PaymentAtomicWriter writer,
        List<AllocationInput> allocations,
        Guid? destination = null,
        IReadOnlyList<PaymentPostingInput>? postings = null)
    {
        var amount = allocations.Sum(x => x.AmountMinor);
        var intent = await writer.ExecuteCreateIntentAsync(Guid.NewGuid(), Guid.NewGuid().ToString("N"), amount,
            "IRR", allocations, null, destination, CancellationToken.None, postings);
        return await writer.ExecuteCaptureIntentAsync(intent.IntentId, Guid.NewGuid().ToString("N"), CancellationToken.None);
    }

    private static async Task ObserveRefundRaceAsync(Func<Task<RefundPaymentAtomicResult>> action)
    {
        try { await action(); }
        catch (Refahi.Modules.Wallets.Application.Contracts.Exceptions.PaymentNotRefundableException) { }
    }

    private static async Task ObserveReserveRaceAsync(Func<Task<CreateIntentAtomicResult>> action)
    {
        try { await action(); }
        catch (Refahi.Modules.Wallets.Application.Contracts.Exceptions.InsufficientFundsException) { }
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
        => await ResetToMigrationAsync(connectionString, null);

    private static async Task ResetToMigrationAsync(string connectionString, string? targetMigration)
    {
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.ExecuteAsync(
                "drop schema if exists wallets cascade; drop table if exists public.\"__EFMigrationsHistory\";"
            );
        }

        var options = new DbContextOptionsBuilder<WalletsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new WalletsDbContext(options);
        if (targetMigration is null)
            await context.Database.MigrateAsync();
        else
            await context.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private static async Task SeedWalletAsync(
        string connectionString,
        Guid walletId,
        long openingAmount,
        short walletType,
        Guid? ownerId = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        var now = DateTimeOffset.UtcNow;
        var ledgerId = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            insert into wallets.wallets
              (wallet_id, "OwnerId", wallet_type, status, currency, created_at, allowed_category_code, contract_expires_at)
            values (@WalletId, @OwnerId, @WalletType, 1, 'IRR', @Now, null, null);
            insert into wallets.wallet_balances
              (wallet_id, available_minor, pending_minor, currency, last_ledger_entry_id, version, updated_at)
            values (@WalletId, @OpeningAmount, 0, 'IRR', @LastLedgerEntryId, 1, @Now);
            """ + (openingAmount > 0
                ? """
                insert into wallets.ledger_entries
                  (ledger_entry_id, wallet_id, operation_id, operation_type, entry_type, amount_minor,
                   available_delta_minor, pending_delta_minor, currency, effective_at, created_at,
                   related_entry_id, relation_type, external_reference, metadata)
                values (@LedgerId, @WalletId, @LedgerId, 1, 1, @OpeningAmount,
                        @OpeningAmount, 0, 'IRR', @Now, @Now, null, 0, 'test-opening-balance', null);
                """
                : string.Empty),
            new
            {
                WalletId = walletId,
                OwnerId = ownerId ?? Guid.NewGuid(),
                WalletType = walletType,
                OpeningAmount = openingAmount,
                LastLedgerEntryId = openingAmount > 0 ? ledgerId : (Guid?)null,
                LedgerId = ledgerId,
                Now = now,
            });
    }

    private static async Task SeedWalletsAsync(string connectionString, params Guid[] walletIds)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        foreach (var walletId in walletIds)
        {
            await connection.ExecuteAsync(
                @"
insert into wallets.wallets
  (wallet_id, ""OwnerId"", wallet_type, status, currency, created_at, allowed_category_code, contract_expires_at)
values
  (@WalletId, @OwnerId, 2, 1, 'IRR', @Now, null, null);
insert into wallets.ledger_entries
  (ledger_entry_id, wallet_id, operation_id, operation_type, entry_type, amount_minor,
   available_delta_minor, pending_delta_minor, currency, effective_at, created_at,
   related_entry_id, relation_type, external_reference, metadata)
values
  (@LedgerId, @WalletId, @OperationId, 1, 1, 1000, 1000, 0, 'IRR', @Now, @Now,
   null, 0, 'test-opening-balance', null);
insert into wallets.wallet_balances
  (wallet_id, available_minor, pending_minor, currency, last_ledger_entry_id, version, updated_at)
values
  (@WalletId, 1000, 0, 'IRR', null, 1, @Now);",
                new
                {
                    WalletId = walletId,
                    OwnerId = Guid.NewGuid(),
                    LedgerId = Guid.NewGuid(),
                    OperationId = Guid.NewGuid(),
                    Now = DateTimeOffset.UtcNow,
                }
            );
        }
    }

    private static async Task SeedProviderWalletAsync(string connectionString, Guid walletId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.ExecuteAsync(
            @"
insert into wallets.wallets
  (wallet_id, ""OwnerId"", wallet_type, status, currency, created_at, allowed_category_code, contract_expires_at)
values
  (@WalletId, @OwnerId, 3, 1, 'IRR', @Now, null, null);
insert into wallets.wallet_balances
  (wallet_id, available_minor, pending_minor, currency, last_ledger_entry_id, version, updated_at)
values
  (@WalletId, 0, 0, 'IRR', null, 1, @Now);",
            new
            {
                WalletId = walletId,
                OwnerId = Guid.NewGuid(),
                Now = DateTimeOffset.UtcNow,
            }
        );
    }

    private static async Task AssertBalancesAsync(
        string connectionString,
        params (Guid WalletId, long Available, long Pending)[] expected
    )
    {
        await using var connection = new NpgsqlConnection(connectionString);
        foreach (var item in expected)
        {
            var balance = await connection.QuerySingleAsync<BalanceRow>(
                @"
select available_minor as Available, pending_minor as Pending
from wallets.wallet_balances where wallet_id = @WalletId;",
                new { item.WalletId }
            );
            Assert.Equal(item.Available, balance.Available);
            Assert.Equal(item.Pending, balance.Pending);
        }
    }

    private static async Task AssertLedgerAsync(
        string connectionString,
        short operationType,
        int expectedCount,
        short relationType = 0
    )
    {
        await using var connection = new NpgsqlConnection(connectionString);
        var count = await connection.ExecuteScalarAsync<int>(
            @"
select count(*) from wallets.ledger_entries
where operation_type = @OperationType and relation_type = @RelationType
  and ((@RelationType = 0 and related_entry_id is null) or (@RelationType <> 0 and related_entry_id is not null));",
            new { OperationType = operationType, RelationType = relationType }
        );
        Assert.Equal(expectedCount, count);
    }

    private static void AssertTimestampsMatch(DateTimeOffset expected, DateTimeOffset actual)
    {
        var difference = (expected - actual).Duration();
        Assert.InRange(difference, TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
    }

    private static async Task AssertNoDriftAsync(
        string connectionString,
        params Guid[] walletIds
    )
    {
        var rebuilder = new BalanceRebuilder(
            connectionString,
            NullLogger<BalanceRebuilder>.Instance
        );
        foreach (var walletId in walletIds)
        {
            var drift = await rebuilder.DetectDriftAsync(walletId, CancellationToken.None);
            Assert.False(drift.Drift.HasDrift);
        }
    }

    private static async Task ObserveTerminalRaceAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            await operation();
        }
        catch (Refahi.Modules.Wallets.Application.Contracts.Exceptions.PaymentIntentStateViolationException)
        {
            // The competing terminal transition won. This is the expected safe outcome.
        }
    }

    private sealed record BalanceRow(long Available, long Pending);
}
