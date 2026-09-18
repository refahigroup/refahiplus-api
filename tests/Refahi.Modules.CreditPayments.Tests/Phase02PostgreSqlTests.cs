using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase02PostgreSqlTests:IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres=new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    public Task InitializeAsync()=>postgres.StartAsync();
    public Task DisposeAsync()=>postgres.DisposeAsync().AsTask();
    private CreditPaymentsDbContext Create()=>new(new DbContextOptionsBuilder<CreditPaymentsDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);

    [Fact]
    public async Task Migration_constraints_rotation_and_concurrency_work_on_real_postgresql()
    {
        await using(var db=Create())await db.Database.MigrateAsync();
        var now=DateTimeOffset.UtcNow;var merchant=Merchant.Create(Guid.NewGuid(),"فروشگاه",now);merchant.Activate(now);var terminal=merchant.AddTerminal("وب","store",100,1000,["https://merchant.example/callback"],now);var secrets=new Pbkdf2CredentialSecretService();var first=secrets.Generate();terminal.AddCredential(first.ClientId,first.Hash,first.Algorithm,first.Version,[CredentialScopes.PaymentCreate],"اول",null,now);
        await using(var db=Create()){db.Merchants.Add(merchant);await db.SaveChangesAsync();}
        await using(var db=Create()){var persisted=await db.TerminalCredentials.SingleAsync();Assert.NotEqual(first.Secret,persisted.SecretHash);Assert.DoesNotContain(first.Secret,persisted.SecretHash);}
        await using(var db=Create()){db.Merchants.Add(Merchant.Create(merchant.SupplierId,"تکراری",now));await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());}
        await using(var connection=new NpgsqlConnection(postgres.GetConnectionString())){await connection.OpenAsync();await using var cmd=new NpgsqlCommand("SELECT COUNT(*) FROM credit_payments.terminal_credentials; SELECT COUNT(*) FROM credit_payments.webhook_endpoints;",connection);Assert.Equal(1L,Convert.ToInt64(await cmd.ExecuteScalarAsync()));}
        await using(var a=Create()){await using var b=Create();var ta=await a.Terminals.SingleAsync();var tb=await b.Terminals.SingleAsync();ta.UpdatePolicy("store.clothing",100,2000,now.AddMinutes(1));await a.SaveChangesAsync();tb.UpdatePolicy("store.food",100,3000,now.AddMinutes(2));await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>b.SaveChangesAsync());}
        await using(var db=Create())await db.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase);
        await using(var connection=new NpgsqlConnection(postgres.GetConnectionString())){await connection.OpenAsync();await using var cmd=new NpgsqlCommand("SELECT to_regclass('credit_payments.merchants')::text",connection);Assert.Equal(DBNull.Value,await cmd.ExecuteScalarAsync());}
    }
}
