using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Infrastructure.Workers;

public sealed class PaymentIntegrityAuditWorker(
    string connectionString,
    ILogger<PaymentIntegrityAuditWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AuditAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Wallet payment integrity audit failed.");
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task AuditAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        var result = await connection.QuerySingleAsync<AuditResult>(new CommandDefinition("""
            with allocation_totals as (
              select intent_id, coalesce(sum(amount_minor),0) amount from wallets.payment_intent_allocations group by intent_id
            ), ledger_totals as (
              select wallet_id,
                coalesce(sum(amount_minor) filter(where operation_type=1),0)
                - coalesce(sum(amount_minor) filter(where operation_type=2),0)
                + coalesce(sum(amount_minor) filter(where operation_type=4),0)
                + coalesce(sum(amount_minor) filter(where operation_type=5),0) available,
                coalesce(sum(amount_minor) filter(where operation_type=2),0)
                - coalesce(sum(amount_minor) filter(where operation_type=3),0)
                - coalesce(sum(amount_minor) filter(where operation_type=4),0) pending
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
              (select count(*) from wallets.payment_intents where status=1 and created_at < now() - interval '30 minutes') as StaleReservedIntents
            """, cancellationToken: ct));

        if (result.TotalProblems > 0)
            logger.LogCritical(
                "Wallet integrity drift detected. AllocationMismatch={AllocationMismatch} OrphanHolds={OrphanHolds} CapturedWithoutPayment={CapturedWithoutPayment} ReleasedWithoutLedger={ReleasedWithoutLedger} ProjectionDrift={ProjectionDrift} StaleReservedIntents={StaleReservedIntents}",
                result.AllocationAmountMismatch, result.OrphanHolds, result.CapturedWithoutPayment,
                result.ReleasedWithoutLedger, result.ProjectionDrift, result.StaleReservedIntents);
        else
            logger.LogInformation("Wallet payment integrity audit completed with zero drift.");
    }

    private sealed record AuditResult(
        long AllocationAmountMismatch,
        long OrphanHolds,
        long CapturedWithoutPayment,
        long ReleasedWithoutLedger,
        long ProjectionDrift,
        long StaleReservedIntents)
    {
        public long TotalProblems => AllocationAmountMismatch + OrphanHolds + CapturedWithoutPayment +
            ReleasedWithoutLedger + ProjectionDrift + StaleReservedIntents;
    }
}
