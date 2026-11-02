using Npgsql;

namespace Refahi.Tests;

internal static class DisposablePostgres
{
    internal static async Task ValidateAndProbeAsync(string connection, string prefix, int? localPort = null)
    {
        var settings = new NpgsqlConnectionStringBuilder(connection);
        var local = settings.Host is "localhost" or "127.0.0.1";
        var ci = Environment.GetEnvironmentVariable("CI") == "true"
            && Environment.GetEnvironmentVariable("REFAHI_DISPOSABLE_CI_DATABASE") == "true"
            && settings.Host == "postgres" && settings.Port == 5432;
        var name = settings.Database ?? "";
        if ((!local && !ci) || (local && localPort.HasValue && settings.Port != localPort)
            || !(name == prefix || name.StartsWith(prefix + "_", StringComparison.Ordinal))
            || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new InvalidOperationException("Only explicitly named disposable local/CI databases are permitted.");

        settings.Database = "postgres";
        await using var admin = new NpgsqlConnection(settings.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT rolcreatedb OR rolsuper FROM pg_roles WHERE rolname=current_user", admin);
        if (await command.ExecuteScalarAsync() is not true)
            throw new InvalidOperationException("Disposable PostgreSQL fixture requires CREATE DATABASE permission.");
    }
}
