using Npgsql;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
namespace Refahi.Modules.Entertainments.Infrastructure.Persistence;

public sealed class PostgresEntertainmentMutationLock(string connectionString) : IEntertainmentMutationLock
{
    public async Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        try
        {
            using var command = new NpgsqlCommand("select pg_advisory_lock(1947592711, hashtext(@id));", connection);
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
                using var command = new NpgsqlCommand("select pg_advisory_unlock(1947592711, hashtext(@id));", connection);
                command.Parameters.AddWithValue("id", id.ToString());
                await command.ExecuteNonQueryAsync();
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}
