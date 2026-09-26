using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Application.Contracts;
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
        var now=DateTimeOffset.UtcNow;var merchant=MerchantTestFactory.Create(Guid.NewGuid(),"فروشگاه",now);merchant.Activate(now);var terminal=merchant.AddTerminal("وب","store",100,1000,["https://merchant.example/callback"],now);var secrets=new Pbkdf2CredentialSecretService();var first=secrets.Generate();terminal.AddCredential(first.ClientId,first.Hash,first.Algorithm,first.Version,[CredentialScopes.PaymentCreate],"اول",null,now);
        await using(var db=Create()){db.Merchants.Add(merchant);await db.SaveChangesAsync();}
        await using(var db=Create()){var persisted=await db.TerminalCredentials.SingleAsync();Assert.NotEqual(first.Secret,persisted.SecretHash);Assert.DoesNotContain(first.Secret,persisted.SecretHash);}
        await using(var db=Create()){db.Merchants.Add(Merchant.Create(merchant.EntityType,merchant.NationalId,"تکراری",null,now));await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());}
        await using(var connection=new NpgsqlConnection(postgres.GetConnectionString())){await connection.OpenAsync();await using var cmd=new NpgsqlCommand("SELECT COUNT(*) FROM credit_payments.terminal_credentials; SELECT COUNT(*) FROM credit_payments.webhook_endpoints;",connection);Assert.Equal(1L,Convert.ToInt64(await cmd.ExecuteScalarAsync()));}
        await using(var a=Create()){await using var b=Create();var ta=await a.Terminals.SingleAsync();var tb=await b.Terminals.SingleAsync();ta.UpdatePolicy("store.clothing",100,2000,now.AddMinutes(1));await a.SaveChangesAsync();tb.UpdatePolicy("store.food",100,3000,now.AddMinutes(2));await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>b.SaveChangesAsync());}
        await using(var db=Create())await db.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase);
        await using(var connection=new NpgsqlConnection(postgres.GetConnectionString())){await connection.OpenAsync();await using var cmd=new NpgsqlCommand("SELECT to_regclass('credit_payments.merchants')::text",connection);Assert.Equal(DBNull.Value,await cmd.ExecuteScalarAsync());}
    }

    [Fact]
    public async Task Terminal_creation_recovers_after_a_concurrent_merchant_update()
    {
        await using (var migration = Create()) await migration.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var merchant = MerchantTestFactory.Create(Guid.NewGuid(), "فروشگاه", now);
        merchant.Activate(now);
        await using (var seed = Create())
        {
            seed.Merchants.Add(merchant);
            await seed.SaveChangesAsync();
        }

        await using var staleDb = Create();
        await using var winnerDb = Create();
        var staleRepo = new CreditPaymentsRepository(staleDb);
        var winnerRepo = new CreditPaymentsRepository(winnerDb);
        var staleMerchant = await staleRepo.GetMerchantAsync(merchant.Id, true, default);
        var winnerMerchant = await winnerRepo.GetMerchantAsync(merchant.Id, true, default);
        Assert.NotNull(staleMerchant);
        Assert.NotNull(winnerMerchant);

        var winnerHandler = new CreateTerminalHandler(winnerRepo);
        await winnerHandler.Handle(new CreateTerminalCommand(merchant.Id, "اول", "store",
            100, 1000, ["https://merchant.example/return"]), default);

        var staleTerminal = staleMerchant.AddTerminal("دوم", "store", 100, 1000,
            ["https://merchant.example/return"], now);
        staleRepo.Add(staleTerminal);
        staleRepo.AddAudit(AuditRecord.Create("terminal.created", "Terminal", Guid.NewGuid(), null, now));
        Assert.False(await staleRepo.TrySaveTerminalCreationAsync(default));
        Assert.Empty(staleDb.ChangeTracker.Entries());

        var handler = new CreateTerminalHandler(staleRepo);
        var created = await handler.Handle(new CreateTerminalCommand(merchant.Id, "دوم", "store",
            100, 1000, ["https://merchant.example/return"]), default);
        Assert.Equal("دوم", created.Name);
        await using var verification = Create();
        Assert.Equal(2, await verification.Terminals.CountAsync());
        Assert.Equal(2, await verification.AuditRecords.CountAsync(x => x.Action == "terminal.created"));
    }

    [Fact]
    public async Task Adding_return_url_to_loaded_terminal_inserts_the_new_row()
    {
        var terminal = await SeedActiveTerminal();
        await using var db = Create();
        var result = await new ReturnUrlHandlers(new CreditPaymentsRepository(db)).Handle(
            new AddReturnUrlCommand(terminal.Id, "https://merchant.example/second", terminal.Version), default);
        Assert.Equal(2, result.ReturnUrls.Count);
        await using var verification = Create();
        Assert.Equal(2, await verification.Set<TerminalReturnUrl>().CountAsync());
    }

    [Fact]
    public async Task Issuing_credential_to_loaded_terminal_inserts_the_new_row()
    {
        var terminal = await SeedActiveTerminal();
        await using var db = Create();
        var result = await new IssueCredentialHandler(new CreditPaymentsRepository(db),
            new Pbkdf2CredentialSecretService()).Handle(
            new IssueCredentialCommand(terminal.Id, "اصلی", [CredentialScopes.PaymentCreate], null), default);
        await using var verification = Create();
        var persisted = await verification.TerminalCredentials.SingleAsync();
        Assert.Equal(result.CredentialId, persisted.Id);
        Assert.NotEqual(result.ClientSecret, persisted.SecretHash);
    }

    [Fact]
    public async Task Configuring_webhook_on_loaded_terminal_inserts_the_new_row()
    {
        var terminal = await SeedActiveTerminal();
        await using var db = Create();
        var result = await new ConfigureWebhookHandler(new CreditPaymentsRepository(db),
            new TestWebhookSecretProtector()).Handle(new ConfigureWebhookCommand(
            terminal.Id, "https://hooks.example.com/cpg", terminal.Version), default);
        await using var verification = Create();
        var persisted = await verification.Set<WebhookEndpoint>().SingleAsync();
        Assert.Equal(result.Webhook.Id, persisted.Id);
        Assert.Equal("encrypted-test-secret", persisted.SigningSecretCiphertext);
    }

    [Fact]
    public async Task Existing_management_handlers_update_and_remove_persisted_rows()
    {
        var terminal = await SeedActiveTerminal();
        await using (var db = Create())
        {
            var added = await new ReturnUrlHandlers(new CreditPaymentsRepository(db)).Handle(
                new AddReturnUrlCommand(terminal.Id, "https://merchant.example/second", terminal.Version), default);
            terminal = (await db.Terminals.SingleAsync(x => x.Id == terminal.Id))!;
            await new ReturnUrlHandlers(new CreditPaymentsRepository(db)).Handle(
                new RemoveReturnUrlCommand(terminal.Id, added.ReturnUrls.Single(x => x.Url.EndsWith("/second")).Id,
                    added.Version), default);
        }
        await using (var db = Create())
        {
            var current = await db.Terminals.SingleAsync(x => x.Id == terminal.Id);
            await new UpdateTerminalPolicyHandler(new CreditPaymentsRepository(db)).Handle(
                new UpdateTerminalPolicyCommand(terminal.Id, "store.food", 100, 2000, current.Version), default);
        }
        Guid credentialId;
        await using (var db = Create())
        {
            var issued = await new IssueCredentialHandler(new CreditPaymentsRepository(db),
                new Pbkdf2CredentialSecretService()).Handle(
                new IssueCredentialCommand(terminal.Id, "اصلی", [CredentialScopes.PaymentCreate], null), default);
            credentialId = issued.CredentialId;
        }
        await using (var db = Create())
            await new RevokeCredentialHandler(new CreditPaymentsRepository(db)).Handle(
                new RevokeCredentialCommand(credentialId), default);
        await using (var db = Create())
        {
            var current = await db.Terminals.SingleAsync(x => x.Id == terminal.Id);
            await new ConfigureWebhookHandler(new CreditPaymentsRepository(db),
                new TestWebhookSecretProtector()).Handle(
                new ConfigureWebhookCommand(terminal.Id, "https://hooks.example.com/first", current.Version), default);
        }
        await using (var db = Create())
        {
            var current = await db.Terminals.SingleAsync(x => x.Id == terminal.Id);
            await new ConfigureWebhookHandler(new CreditPaymentsRepository(db),
                new TestWebhookSecretProtector()).Handle(
                new ConfigureWebhookCommand(terminal.Id, "https://hooks.example.com/second", current.Version), default);
        }
        await using (var db = Create())
        {
            var current = await db.Terminals.SingleAsync(x => x.Id == terminal.Id);
            await new DisableWebhookHandler(new CreditPaymentsRepository(db)).Handle(
                new DisableWebhookCommand(terminal.Id, current.Version), default);
        }
        await using (var db = Create())
        {
            var current = await db.Terminals.SingleAsync(x => x.Id == terminal.Id);
            await new ChangeTerminalStatusHandler(new CreditPaymentsRepository(db)).Handle(
                new ChangeTerminalStatusCommand(terminal.Id, "suspend", current.Version), default);
        }
        await using var verification = Create();
        var persisted = await verification.Terminals.Include(x => x.ReturnUrls)
            .Include(x => x.WebhookEndpoint).SingleAsync(x => x.Id == terminal.Id);
        Assert.Single(persisted.ReturnUrls);
        Assert.Equal("store.food", persisted.ApprovedCategoryCode);
        Assert.Equal(TerminalStatus.Suspended, persisted.Status);
        Assert.Equal(WebhookEndpointStatus.Disabled, persisted.WebhookEndpoint!.Status);
        Assert.Equal(2, persisted.WebhookEndpoint.SigningSecretVersion);
        Assert.Equal(CredentialStatus.Revoked,
            (await verification.TerminalCredentials.SingleAsync(x => x.Id == credentialId)).Status);
    }

    [Fact]
    public async Task Concurrent_management_update_returns_conflict_without_audit()
    {
        var terminal = await SeedActiveTerminal();
        await using var staleDb = Create();
        var staleRepo = new CreditPaymentsRepository(staleDb);
        var stale = await staleRepo.GetTerminalAsync(terminal.Id, true, default);
        Assert.NotNull(stale);
        await using (var winner = Create())
        {
            var current = await winner.Terminals.SingleAsync(x => x.Id == terminal.Id);
            current.UpdatePolicy("store.food", 100, 2000, DateTimeOffset.UtcNow);
            await winner.SaveChangesAsync();
        }
        var error = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            new UpdateTerminalPolicyHandler(staleRepo).Handle(new UpdateTerminalPolicyCommand(
                terminal.Id, "store.clothing", 100, 3000, stale.Version), default));
        Assert.Equal(409, error.StatusCode);
        Assert.Empty(staleDb.ChangeTracker.Entries());
        await using var verification = Create();
        Assert.Equal("store.food", (await verification.Terminals.SingleAsync(x => x.Id == terminal.Id)).ApprovedCategoryCode);
        Assert.Equal(0, await verification.AuditRecords.CountAsync(x => x.Action == "terminal.policy.updated"));
    }

    private async Task<Terminal> SeedActiveTerminal()
    {
        await using var db = Create();
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var merchant = MerchantTestFactory.Create(Guid.NewGuid(), "فروشگاه", now);
        merchant.Activate(now);
        var terminal = merchant.AddTerminal("وب", "store", 100, 1000,
            ["https://merchant.example/return"], now);
        db.Merchants.Add(merchant);
        await db.SaveChangesAsync();
        return terminal;
    }

    private sealed class TestWebhookSecretProtector : IWebhookSecretProtector
    {
        public (string Secret, string Ciphertext) GenerateAndProtect() =>
            ("test-secret", "encrypted-test-secret");
        public bool TryUnprotect(string ciphertext, out string secret)
        {
            secret = "test-secret";
            return ciphertext == "encrypted-test-secret";
        }
    }
}
