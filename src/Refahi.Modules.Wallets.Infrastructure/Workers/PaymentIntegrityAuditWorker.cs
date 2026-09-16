using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;

namespace Refahi.Modules.Wallets.Infrastructure.Workers;

public sealed class PaymentIntegrityAuditWorker(
    string connectionString,
    WalletReservationOptions options,
    ILogger<PaymentIntegrityAuditWorker> logger
) : BackgroundService
{
    private readonly PaymentAtomicWriter _paymentWriter = new(connectionString);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AuditAsync(stoppingToken);
                await ReleaseExpiredReservationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Wallet payment integrity audit failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(options.AuditIntervalMinutes), stoppingToken);
        }
    }

    private async Task AuditAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        var result = await connection.QuerySingleAsync<AuditResult>(
            new CommandDefinition(
                """
                with allocation_totals as (
                  select intent_id, coalesce(sum(amount_minor),0) amount from wallets.payment_intent_allocations group by intent_id
                ), ledger_totals as (
                  select wallet_id,
                    coalesce(sum(available_delta_minor),0) available,
                    coalesce(sum(pending_delta_minor),0) pending
                  from wallets.ledger_entries group by wallet_id
                )
                select
                  (select count(*) from wallets.payment_intents i left join allocation_totals a on a.intent_id=i.intent_id
                    where i.amount_minor <> coalesce(a.amount,0)) as AllocationAmountMismatch,
                  (select count(*) from wallets.ledger_entries l
                    join wallets.payment_intents i on l.external_reference='intent:' || i.intent_id::text
                    where l.operation_type=2 and l.entry_type=3 and not exists (
                      select 1 from wallets.payment_intent_allocations a where a.intent_id=i.intent_id and a.wallet_id=l.wallet_id)) as OrphanHolds,
                  (select count(*) from wallets.payment_intents i where i.status=2 and (
                    not exists(select 1 from wallets.payments p where p.intent_id=i.intent_id) or
                    not exists(select 1 from wallets.payments p join wallets.payment_allocations a on a.payment_id=p.payment_id where p.intent_id=i.intent_id))) as CapturedWithoutPayment,
                  (select count(*) from wallets.payment_intents i where i.status=3 and exists(
                    select 1 from wallets.ledger_entries h where h.external_reference='intent:' || i.intent_id::text
                      and h.operation_type=2 and not exists(select 1 from wallets.ledger_entries r where r.related_entry_id=h.ledger_entry_id and r.operation_type=4))) as ReleasedWithoutLedger,
                  (select count(*) from wallets.wallet_balances b left join ledger_totals l on l.wallet_id=b.wallet_id
                    where b.available_minor <> coalesce(l.available,0) or b.pending_minor <> coalesce(l.pending,0)) as ProjectionDrift,
                  (select count(*) from wallets.payment_intents where status=1 and created_at < @ExpiresBefore) as StaleReservedIntents,
                  (select count(*) from wallets.ledger_entries le left join wallets.wallets w on w.wallet_id=le.wallet_id
                    where w.wallet_id is null) as LedgerWithoutWallet,
                  (select count(*) from wallets.ledger_entries le left join wallets.wallet_balances b on b.wallet_id=le.wallet_id
                    where b.wallet_id is null) as LedgerWithoutBalance,
                  (select count(*) from wallets.wallet_balances b left join wallets.wallets w on w.wallet_id=b.wallet_id
                    where w.wallet_id is null) as BalanceWithoutWallet,
                  (select count(*) from wallets.wallets w left join wallets.wallet_balances b on b.wallet_id=w.wallet_id
                    where w.status=1 and b.wallet_id is null) as ActiveWalletWithoutBalance
                """,
                new { ExpiresBefore = DateTimeOffset.UtcNow.AddMinutes(-options.ExpirationMinutes) },
                cancellationToken: ct
            )
        );

        if (result.TotalProblems > 0)
            logger.LogCritical(
                "Wallet integrity drift detected. AllocationMismatch={AllocationMismatch} OrphanHolds={OrphanHolds} CapturedWithoutPayment={CapturedWithoutPayment} ReleasedWithoutLedger={ReleasedWithoutLedger} ProjectionDrift={ProjectionDrift} StaleReservedIntents={StaleReservedIntents} LedgerWithoutWallet={LedgerWithoutWallet} LedgerWithoutBalance={LedgerWithoutBalance} BalanceWithoutWallet={BalanceWithoutWallet} ActiveWalletWithoutBalance={ActiveWalletWithoutBalance}",
                result.AllocationAmountMismatch,
                result.OrphanHolds,
                result.CapturedWithoutPayment,
                result.ReleasedWithoutLedger,
                result.ProjectionDrift,
                result.StaleReservedIntents,
                result.LedgerWithoutWallet,
                result.LedgerWithoutBalance,
                result.BalanceWithoutWallet,
                result.ActiveWalletWithoutBalance
            );
        else
            logger.LogInformation("Wallet payment integrity audit completed with zero drift.");
    }

    private async Task ReleaseExpiredReservationsAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        var intentIds = (await connection.QueryAsync<Guid>(
            new CommandDefinition(
                """
                select intent_id
                from wallets.payment_intents
                where status = 1 and created_at < @ExpiresBefore
                order by created_at
                limit @BatchSize
                """,
                new
                {
                    ExpiresBefore = DateTimeOffset.UtcNow.AddMinutes(-options.ExpirationMinutes),
                    BatchSize = options.ReleaseBatchSize,
                },
                cancellationToken: ct
            )
        )).ToList();

        foreach (var intentId in intentIds)
        {
            try
            {
                var result = await _paymentWriter.ExecuteReleaseIntentAsync(
                    intentId,
                    $"wallet-expiration-{intentId:N}",
                    ct
                );
                logger.LogInformation(
                    "Expired wallet reservation release attempted. PaymentIntentId={PaymentIntentId} Outcome={Outcome}",
                    intentId,
                    result.Outcome
                );
            }
            catch (PaymentIntentStateViolationException)
            {
                logger.LogInformation(
                    "Expired wallet reservation was concurrently finalized. PaymentIntentId={PaymentIntentId}",
                    intentId
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to release expired wallet reservation. PaymentIntentId={PaymentIntentId}",
                    intentId
                );
            }
        }
    }

    private sealed record AuditResult(
        long AllocationAmountMismatch,
        long OrphanHolds,
        long CapturedWithoutPayment,
        long ReleasedWithoutLedger,
        long ProjectionDrift,
        long StaleReservedIntents,
        long LedgerWithoutWallet,
        long LedgerWithoutBalance,
        long BalanceWithoutWallet,
        long ActiveWalletWithoutBalance
    )
    {
        public long TotalProblems =>
            AllocationAmountMismatch
            + OrphanHolds
            + CapturedWithoutPayment
            + ReleasedWithoutLedger
            + ProjectionDrift
            + StaleReservedIntents
            + LedgerWithoutWallet
            + LedgerWithoutBalance
            + BalanceWithoutWallet
            + ActiveWalletWithoutBalance;
    }
}
