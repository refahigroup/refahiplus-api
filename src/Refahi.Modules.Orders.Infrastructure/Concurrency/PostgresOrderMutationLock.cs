using Npgsql;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Infrastructure.Concurrency;

internal sealed class PostgresOrderMutationLock(string connectionString) : IOrderMutationLock
{
    private const int LockNamespace = 1_330_794_578;

    public async Task<IAsyncDisposable> AcquireAsync(Guid orderId, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        try
        {
            await using var command = new NpgsqlCommand(
                "select pg_advisory_lock(@namespace, hashtext(@order_id::text));", connection);
            command.Parameters.AddWithValue("namespace", LockNamespace);
            command.Parameters.AddWithValue("order_id", orderId);
            await command.ExecuteNonQueryAsync(ct);
            return new Handle(connection, orderId);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Handle(NpgsqlConnection connection, Guid orderId) : IAsyncDisposable
    {
        private int _disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try
            {
                await using var command = new NpgsqlCommand(
                    "select pg_advisory_unlock(@namespace, hashtext(@order_id::text));", connection);
                command.Parameters.AddWithValue("namespace", LockNamespace);
                command.Parameters.AddWithValue("order_id", orderId);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
