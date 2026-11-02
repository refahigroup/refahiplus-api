#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Refahi.Modules.Wallets.Application.Contracts.Queries;
namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Repositories;

public sealed class OrderPaymentRecoveryReader(string connectionString) : IOrderPaymentRecoveryReader
{
    public async Task<OrderPaymentRecovery?> FindAsync(Guid orderId, string key, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.QuerySingleOrDefaultAsync<OrderPaymentRecovery>(new CommandDefinition("""
            select i.intent_id as IntentId,i.status as Status,p.payment_id as PaymentId,
                exists(select 1 from wallets.refunds r where r.payment_id=p.payment_id) as Refunded
            from wallets.payment_intents i left join wallets.payments p on p.intent_id=i.intent_id
            where i.order_id=@OrderId and i.idempotency_key=@Key
            """, new { OrderId = orderId, Key = key }, cancellationToken: ct));
    }
}
