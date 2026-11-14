using Npgsql;
using Refahi.Modules.Pools.Application.Contracts;
namespace Refahi.Modules.Pools.Infrastructure.Persistence;
public sealed class PostgresPoolMutationLock(string connectionString) : IPoolMutationLock
{
    public async Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(ct);
            using var command = new NpgsqlCommand("select pg_advisory_lock(1347374924, hashtext(@id));", connection);
            command.Parameters.AddWithValue("id", id.ToString()); await command.ExecuteNonQueryAsync(ct);
            return new Handle(connection, id);
        }
        catch { await connection.DisposeAsync(); throw; }
    }
    private sealed class Handle(NpgsqlConnection connection, Guid id) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { using var command = new NpgsqlCommand("select pg_advisory_unlock(1347374924, hashtext(@id));", connection);
                command.Parameters.AddWithValue("id", id.ToString()); await command.ExecuteNonQueryAsync(); }
            finally { await connection.DisposeAsync(); }
        }
    }
}
