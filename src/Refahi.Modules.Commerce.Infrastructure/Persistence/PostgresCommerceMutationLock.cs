using Npgsql;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
namespace Refahi.Modules.Commerce.Infrastructure.Persistence;

public sealed class PostgresCommerceMutationLock(string connectionString) : ICommerceMutationLock
{
    public async Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        try
        {
            using var command = new NpgsqlCommand("select pg_advisory_lock(1330794579, hashtext(@id));", connection);
            command.Parameters.AddWithValue("id", id.ToString());
            await command.ExecuteNonQueryAsync(ct);
            return new Handle(connection, id);
        }
        catch { await connection.DisposeAsync(); throw; }
    }
    private sealed class Handle(NpgsqlConnection connection, Guid id) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                using var command = new NpgsqlCommand("select pg_advisory_unlock(1330794579, hashtext(@id));", connection);
                command.Parameters.AddWithValue("id", id.ToString());
                await command.ExecuteNonQueryAsync();
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}
