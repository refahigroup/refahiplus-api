using Npgsql;
using Refahi.Modules.Cinema.Application.Contracts;
namespace Refahi.Modules.Cinema.Infrastructure.Persistence;

public sealed class PostgresCinemaMutationLock(string connectionString) : ICinemaMutationLock
{
    public async Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(ct);
            using var command = new NpgsqlCommand("select pg_advisory_lock(1128877637, hashtext(@id));", connection);
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
                using var command = new NpgsqlCommand("select pg_advisory_unlock(1128877637, hashtext(@id));", connection);
                command.Parameters.AddWithValue("id", id.ToString());
                await command.ExecuteNonQueryAsync();
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}
