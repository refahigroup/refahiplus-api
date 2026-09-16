using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Context;
using Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Refahi.Modules.PaymentGateway.Tests;

public sealed class PaymentGatewayPostgresTests
{
    private const string Variable = "REFAHI_PAYMENT_GATEWAY_TEST_CONNECTION";

    [SkippableFact]
    public async Task EmptyDatabase_MigrationCreatesFundingSafetyConstraints()
    {
        var cs = RequireConnection();
        await ResetAsync(cs);
        await MigrateAsync(cs);
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();

        var constraints = new HashSet<string>();
        await using (var command = new NpgsqlCommand(
            "select conname from pg_constraint where connamespace='payment_gateway'::regnamespace", connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) constraints.Add(reader.GetString(0));

        Assert.Contains("ck_pg_sessions_amount_positive", constraints);
        Assert.Contains("ck_pg_sessions_initiation_hash_pair", constraints);
    }

    [SkippableFact]
    public async Task PreviousSchema_MigratesHistoricalNullableIdentityWithoutFabrication()
    {
        var cs = RequireConnection();
        await ResetAsync(cs);
        var options = Options(cs);
        await using (var db = new PaymentGatewayDbContext(options))
            await db.GetService<IMigrator>().MigrateAsync("20260510055449_InitialPaymentGatewaySchema");

        var id = Guid.NewGuid();
        await ExecuteAsync(cs, """
            insert into payment_gateway.sessions
              (session_id,user_id,wallet_id,amount_minor,currency,provider,status,return_base_url,
               initiated_at,expires_at,provider_ref_num)
            values (@id,@user,@wallet,1000,'IRR',2,5,'/result',now(),now()+interval '1 hour',null)
            """, ("id", id), ("user", Guid.NewGuid()), ("wallet", Guid.NewGuid()));

        await MigrateAsync(cs);
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select provider_attempt_id,provider_transaction_id from payment_gateway.sessions where session_id=@id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.IsDBNull(0));
        Assert.True(reader.IsDBNull(1));
    }

    [SkippableFact]
    public async Task ProviderTransaction_IsUniqueAcrossSessionsOnSameWallet()
    {
        var cs = RequireConnection();
        await AssertProviderTransactionUniqueAsync(cs, sameWallet: true);
    }

    [SkippableFact]
    public async Task ProviderTransaction_IsUniqueAcrossDifferentWallets()
    {
        var cs = RequireConnection();
        await AssertProviderTransactionUniqueAsync(cs, sameWallet: false);
    }

    private static async Task AssertProviderTransactionUniqueAsync(string cs, bool sameWallet)
    {
        await ResetAsync(cs);
        await MigrateAsync(cs);
        var walletId = Guid.NewGuid();
        var first = Create("key-a", "hash-a", walletId);
        var second = Create("key-b", "hash-b", sameWallet ? walletId : Guid.NewGuid());
        first.MarkAsTokenReceived("token-a", "attempt-a");
        second.MarkAsTokenReceived("token-b", "attempt-b");

        await using (var db = new PaymentGatewayDbContext(Options(cs)))
        {
            db.Sessions.AddRange(first, second);
            await db.SaveChangesAsync();
        }
        first.BindProviderTransaction("global-txn");
        await using (var db = new PaymentGatewayDbContext(Options(cs)))
        {
            db.Attach(first);
            db.Entry(first).Property(x => x.ProviderTransactionId).IsModified = true;
            await db.SaveChangesAsync();
        }
        second.BindProviderTransaction("global-txn");
        await using var secondDb = new PaymentGatewayDbContext(Options(cs));
        secondDb.Attach(second);
        secondDb.Entry(second).Property(x => x.ProviderTransactionId).IsModified = true;
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => secondDb.SaveChangesAsync());
        Assert.IsType<PostgresException>(error.InnerException);
    }

    [SkippableFact]
    public async Task ConcurrentClaims_AllowOneCallbackOrRecoveryOwner()
    {
        var cs = RequireConnection();
        await ResetAsync(cs);
        await MigrateAsync(cs);
        var session = Create("claim-key", "claim-hash");
        session.MarkAsTokenReceived("token", "attempt");
        await using (var db = new PaymentGatewayDbContext(Options(cs)))
        {
            db.Sessions.Add(session);
            await db.SaveChangesAsync();
        }

        var now = DateTimeOffset.UtcNow;
        async Task<bool> ClaimAsync()
        {
            await using var db = new PaymentGatewayDbContext(Options(cs));
            var repo = new PaymentGatewaySessionRepository(db);
            return await repo.TryClaimAsync(session.Id,
                [PaymentSessionStatus.TokenReceived], now, now.AddMinutes(2));
        }

        var results = await Task.WhenAll(ClaimAsync(), ClaimAsync());
        Assert.Single(results, x => x);
    }

    [SkippableFact]
    public async Task DatabaseChecks_RejectUnpairedHashAndInvalidStatus()
    {
        var cs = RequireConnection();
        await ResetAsync(cs);
        await MigrateAsync(cs);

        var unpaired = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(cs, """
            insert into payment_gateway.sessions
              (session_id,user_id,wallet_id,amount_minor,currency,provider,status,return_base_url,
               initiated_at,expires_at,initiation_request_hash)
            values (@id,@user,@wallet,1000,'IRR',2,1,'/result',now(),now()+interval '1 hour','hash')
            """, ("id", Guid.NewGuid()), ("user", Guid.NewGuid()), ("wallet", Guid.NewGuid())));
        Assert.Equal(PostgresErrorCodes.CheckViolation, unpaired.SqlState);

        var invalidStatus = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(cs, """
            insert into payment_gateway.sessions
              (session_id,user_id,wallet_id,amount_minor,currency,provider,status,return_base_url,
               initiated_at,expires_at)
            values (@id,@user,@wallet,1000,'IRR',2,99,'/result',now(),now()+interval '1 hour')
            """, ("id", Guid.NewGuid()), ("user", Guid.NewGuid()), ("wallet", Guid.NewGuid())));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidStatus.SqlState);
    }

    [SkippableFact]
    public async Task Rollback_IsSafeBeforeNewData_AndBlockedAfterFundingSafetyData()
    {
        var cs = RequireConnection();
        await ResetAsync(cs);
        await MigrateAsync(cs);
        var options = Options(cs);
        await using (var clean = new PaymentGatewayDbContext(options))
            await clean.GetService<IMigrator>().MigrateAsync("20260510055449_InitialPaymentGatewaySchema");

        await MigrateAsync(cs);
        await using (var db = new PaymentGatewayDbContext(options))
        {
            db.Sessions.Add(Create("rollback-key", "rollback-hash"));
            await db.SaveChangesAsync();
        }

        await using var unsafeRollback = new PaymentGatewayDbContext(options);
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            unsafeRollback.GetService<IMigrator>()
                .MigrateAsync("20260510055449_InitialPaymentGatewaySchema"));
        Assert.Contains("forward-only", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PaymentGatewaySession Create(string key, string hash, Guid? walletId = null) =>
        PaymentGatewaySession.Create(Guid.NewGuid(), Guid.NewGuid(), walletId ?? Guid.NewGuid(),
            1000, "IRR", PaymentGatewayProviderType.Jibit, "/result",
            initiationIdempotencyKey: key, initiationRequestHash: hash,
            initiationRequestHashVersion: 1);

    private static string RequireConnection()
    {
        var cs = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(cs))
            Skip.If(true, $"PostgreSQL test skipped: {Variable} is not configured.");
        var builder = new NpgsqlConnectionStringBuilder(cs);
        Assert.Contains("test", builder.Database, StringComparison.OrdinalIgnoreCase);
        return cs!;
    }

    private static DbContextOptions<PaymentGatewayDbContext> Options(string cs) =>
        new DbContextOptionsBuilder<PaymentGatewayDbContext>().UseNpgsql(cs).Options;

    private static async Task ResetAsync(string cs)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "drop schema if exists payment_gateway cascade; drop table if exists public.\"__EFMigrationsHistory\";", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task MigrateAsync(string cs)
    {
        await using var db = new PaymentGatewayDbContext(Options(cs));
        await db.Database.MigrateAsync();
    }

    private static async Task ExecuteAsync(string cs, string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }
}
