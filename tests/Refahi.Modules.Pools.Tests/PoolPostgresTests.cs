using Dapper;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Refahi.Modules.Pools.Api;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Identity.Application.Contracts.AuthorizationGrants;
using Refahi.Modules.Pools.Infrastructure.Persistence;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Context;
using Xunit;

namespace Refahi.Modules.Pools.Tests;
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REFAHI_POOLS_TEST_CONNECTION"))) Skip = "Set REFAHI_POOLS_TEST_CONNECTION to an isolated PostgreSQL test database."; }
}
[CollectionDefinition("PoolsPostgres", DisableParallelization = true)]
public sealed class PoolsPostgresCollection;
[Collection("PoolsPostgres")]
public sealed class PoolPostgresTests
{
    private static string Connection => Environment.GetEnvironmentVariable("REFAHI_POOLS_TEST_CONNECTION")!;
    private static ServiceProvider Services()
    {
        Assert.Contains("test",new NpgsqlConnectionStringBuilder(Connection).Database!,StringComparison.OrdinalIgnoreCase);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["ConnectionStringName"]="Test",["ConnectionStrings:Test"]=Connection,["Pools:CatalogEnabled"]="true",["Pools:PurchaseEnabled"]="true",
            ["Pools:PoolTicket:PriceUnit"]="TOMAN"}).Build();
        var s=new ServiceCollection();s.AddSingleton<RefundFault>();s.AddLogging();s.AddSingleton<IConfiguration>(config);s.AddSingleton<IHostEnvironment>(new TestEnvironment("Test"));
        s.RegisterPoolsModule(config);
        Refahi.Modules.Orders.Application.DI.RegisterApplication(s,config);Refahi.Modules.Orders.Infrastructure.DI.RegisterInfrastructure(s,config);
        Refahi.Modules.Wallets.Application.DI.RegisterApplication(s,config);Refahi.Modules.Wallets.Infrastructure.DI.RegisterInfrastructure(s,config);
        s.RemoveAll<IPoolProvider>();s.AddSingleton<TestPoolProvider>();s.AddScoped<IPoolProvider>(sp=>sp.GetRequiredService<TestPoolProvider>());
        s.AddSingleton<TestClock>();s.AddSingleton<TimeProvider>(sp=>sp.GetRequiredService<TestClock>());
        s.AddSingleton<GrantState>();s.AddScoped<IRequestHandler<GetAuthorizationGrantsByIssuerQuery,IReadOnlyList<AuthorizationGrantDto>>,AllGrantsHandler>();s.AddScoped<IRequestHandler<GetActiveAuthorizationGrantsQuery,IReadOnlyList<AuthorizationGrantDto>>,GrantHandler>();
        s.AddTransient(typeof(IPipelineBehavior<,>),typeof(ValidationPipeline<,>));
        return s.BuildServiceProvider();
    }
    private static async Task Migrate(ServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PoolsDbContext>().Database.MigrateAsync();
        await using var wallets = new WalletsDbContext(new DbContextOptionsBuilder<WalletsDbContext>().UseNpgsql(Connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).Options);
        await wallets.Database.MigrateAsync();
        // Existing Orders migration backfills historical Store owners; this isolated database has no Store orders.
        await using (var connection = new NpgsqlConnection(Connection))
            await connection.ExecuteAsync("create schema if not exists store; create table if not exists store.shops (\"Id\" uuid primary key, \"SupplierId\" uuid);");
        await using var orders = new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(Connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).Options);
        await orders.Database.MigrateAsync();
    }
    private static async Task<T> Send<T>(ServiceProvider services, IRequest<T> request)
    { using var scope = services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request); }
    private static async Task<(Guid Org, Guid Personal)> SeedWallets(Guid user, string category = "entertainment")
    {
        var org = Guid.NewGuid(); var personal = Guid.NewGuid(); await using var c = new NpgsqlConnection(Connection);
        foreach (var pair in new[] { (org, 4), (personal, 2) })
            await c.ExecuteAsync("""
                insert into wallets.wallets (wallet_id,"OwnerId",wallet_type,status,currency,created_at,allowed_category_code,contract_expires_at)
                values (@Id,@User,@Kind,1,'IRR',@Now,@Category,@Expires);
                insert into wallets.wallet_balances (wallet_id,available_minor,pending_minor,currency,last_ledger_entry_id,version,updated_at)
                values (@Id,2000000,0,'IRR',null,1,@Now);
                """, new { Id = pair.Item1, User = user, Kind = (short)pair.Item2, Now = DateTimeOffset.UtcNow,
                    Category = pair.Item2 == 4 ? category : null, Expires = pair.Item2 == 4 ? DateTimeOffset.UtcNow.AddDays(10) : (DateTimeOffset?)null });
        return (org, personal);
    }

    private static async Task<PoolChoice[]> Catalogue(ServiceProvider services)
    {
        using(var scope=services.CreateScope()){var m=scope.ServiceProvider.GetRequiredService<IMediator>();await m.Send(new SyncPoolCatalogCommand(true));await m.Send(new SyncPoolCatalogCommand(false));}
        var sessions=await Send(services,new ListPoolSessionsQuery(new(Limit:50)));
        services.GetRequiredService<GrantState>().Pool=sessions.Items[0].PoolId;
        return sessions.Items.Take(2).Select(x=>new PoolChoice(x.Id,1,1)).ToArray();
    }
    private static async Task<(PoolPurchaseDto Purchase,PoolPurchaseCheckoutDto Checkout)> Checkout(ServiceProvider s,Guid user)
    {
        var choices=await Catalogue(s);var quote=await Send(s,new QuotePoolPurchaseQuery(choices));
        var p=await Send(s,new CreatePoolPurchaseCommand(user,choices,quote.TotalMinor,Guid.NewGuid().ToString("N")));
        return(p,await Send(s,new CheckoutPoolPurchaseCommand(p.Id,user,p.Version,p.TotalMinor)));
    }
    private static async Task Reconcile(ServiceProvider s,Guid id){using var scope=s.CreateScope();await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ReconcilePoolPurchaseCommand(id));}
    [PostgresFact]
    public async Task Grouped_purchase_real_split_payment_replay_and_refund_original_allocations()
    {
        await using var s=Services();await Migrate(s);var user=Guid.NewGuid();var wallets=await SeedWallets(user);var (p,c)=await Checkout(s,user);
        Assert.Equal(2,p.Requests.Count);Assert.Equal(1500000,p.TotalMinor);Assert.All(p.Requests,x=>Assert.Null(x.Code));
        var pay=new PayOrderCommand(c.OrderId,user,"User",[new(wallets.Org,600000),new(wallets.Personal,900000)],"pay");
        var paid=await Send(s,pay);Assert.Equal(paid.PaymentId,(await Send(s,pay with{IdempotencyKey="replay"})).PaymentId);
        await Reconcile(s,p.Id);p=await Send(s,new GetPoolPurchaseQuery(p.Id,user));Assert.Equal("Paid",p.Status);Assert.All(p.Requests,x=>Assert.Equal(32,x.Code!.Length));
        var codes=p.Requests.Select(x=>x.Code).ToArray();await Reconcile(s,p.Id);Assert.Equal(codes,(await Send(s,new GetPoolPurchaseQuery(p.Id,user))).Requests.Select(x=>x.Code));
        var cancelled=await Send(s,new CancelPoolPurchaseCommand(p.Id,user));Assert.Equal("Refunded",cancelled.Status);Assert.All(cancelled.Requests,x=>Assert.Null(x.Code));
        await Send(s,new CancelPoolPurchaseCommand(p.Id,user));await using var db=new NpgsqlConnection(Connection);
        foreach(var id in new[]{wallets.Org,wallets.Personal})Assert.Equal(2000000,await db.ExecuteScalarAsync<long>("select available_minor from wallets.wallet_balances where wallet_id=@Id",new{Id=id}));
    }
    [PostgresFact]
    public async Task Concurrent_create_checkout_and_payload_conflict_are_idempotent()
    {
        await using var s=Services();await Migrate(s);var user=Guid.NewGuid();var choices=await Catalogue(s);var quote=await Send(s,new QuotePoolPurchaseQuery(choices));
        var command=new CreatePoolPurchaseCommand(user,choices,quote.TotalMinor,Guid.NewGuid().ToString("N"));
        var purchases=await Task.WhenAll(Send(s,command),Send(s,command));Assert.Equal(purchases[0].Id,purchases[1].Id);
        var p=purchases[0];var checkout=new CheckoutPoolPurchaseCommand(p.Id,user,p.Version,p.TotalMinor);
        var orders=await Task.WhenAll(Send(s,checkout),Send(s,checkout));Assert.Equal(orders[0].OrderId,orders[1].OrderId);
        Assert.Equal("IDEMPOTENCY_MISMATCH",(await Assert.ThrowsAsync<PoolException>(()=>Send(s,command with{ExpectedTotalMinor=1}))).Code);
        await Assert.ThrowsAsync<PoolException>(()=>Send(s,new GetPoolPurchaseQuery(p.Id,Guid.NewGuid())));
    }
    [PostgresFact]
    public async Task Lost_attachment_price_change_and_expiry_cannot_debit()
    {
        await using var s=Services();await Migrate(s);var user=Guid.NewGuid();var wallets=await SeedWallets(user);var(p,c)=await Checkout(s,user);
        using(var scope=s.CreateScope())await scope.ServiceProvider.GetRequiredService<PoolsDbContext>().Purchases.Where(x=>x.Id==p.Id).ExecuteUpdateAsync(x=>x.SetProperty(v=>v.OrderId,(Guid?)null).SetProperty(v=>v.OrderNumber,(string?)null));
        await Reconcile(s,p.Id);Assert.Equal(c.OrderId,(await Send(s,new GetPoolPurchaseQuery(p.Id,user))).OrderId);
        s.GetRequiredService<TestPoolProvider>().PriceDelta=10;
        await Assert.ThrowsAsync<PoolException>(()=>Send(s,new PayOrderCommand(c.OrderId,user,"User",[new(wallets.Personal,p.TotalMinor)],"changed")));
        s.GetRequiredService<TestPoolProvider>().PriceDelta=0;
        using(var scope=s.CreateScope())await scope.ServiceProvider.GetRequiredService<PoolsDbContext>().Purchases.Where(x=>x.Id==p.Id).ExecuteUpdateAsync(x=>x.SetProperty(v=>v.PayableUntil,DateTimeOffset.UtcNow.AddMinutes(-1)));
        await Assert.ThrowsAsync<PoolException>(()=>Send(s,new PayOrderCommand(c.OrderId,user,"User",[new(wallets.Personal,p.TotalMinor)],"expired")));
        await Reconcile(s,p.Id);await using var db=new NpgsqlConnection(Connection);Assert.Equal(2000000,await db.ExecuteScalarAsync<long>("select available_minor from wallets.wallet_balances where wallet_id=@Id",new{Id=wallets.Personal}));
    }
    [PostgresFact]
    public async Task Grant_time_window_concurrent_redemption_and_cancellation_are_serialized()
    {
        await using var s=Services();await Migrate(s);var user=Guid.NewGuid();var wallets=await SeedWallets(user);var(p,c)=await Checkout(s,user);
        await Send(s,new PayOrderCommand(c.OrderId,user,"User",[new(wallets.Personal,p.TotalMinor)],"pay"));await Reconcile(s,p.Id);
        p=await Send(s,new GetPoolPurchaseQuery(p.Id,user));var v=p.Requests[0];var op=Guid.NewGuid();var grants=s.GetRequiredService<GrantState>();grants.User=op;grants.Pool=v.PoolId;
        await Assert.ThrowsAsync<PoolException>(()=>Send(s,new ValidatePoolVoucherQuery(op,Guid.NewGuid(),v.Code!)));
        Assert.False((await Send(s,new ValidatePoolVoucherQuery(op,v.PoolId,v.Code!))).CanRedeem);
        s.GetRequiredService<TestClock>().Now=v.StartsAtUtc;var command=new RedeemPoolVoucherCommand(op,v.PoolId,v.Code!,"redeem");
        var results=await Task.WhenAll(Send(s,command),Send(s,command));Assert.All(results,x=>Assert.Equal("Redeemed",x.Status));
        await Assert.ThrowsAsync<Refahi.Modules.Orders.Domain.Exceptions.OrderStateConflictException>(()=>Send(s,new CancelPoolPurchaseCommand(p.Id,user)));
        grants.Active=false;await Assert.ThrowsAsync<PoolException>(()=>Send(s,command));
    }
    [PostgresFact]
    public async Task Catalogue_stable_ids_complete_only_deletion_and_postgres_lock_cancellation()
    {
        await using var s=Services();await Migrate(s);var first=await Catalogue(s);var again=await Catalogue(s);Assert.Equal(first,again);
        var provider=s.GetRequiredService<TestPoolProvider>();provider.FailSessions=true;
        using(var scope=s.CreateScope())await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SyncPoolCatalogCommand(false));
        Assert.Equal(2,(await Send(s,new ListPoolSessionsQuery(new()))).Total);
        provider.FailSessions=false;provider.OnlyOne=true;await Catalogue(s);Assert.Equal(1,(await Send(s,new ListPoolSessionsQuery(new()))).Total);
        using var locked=s.CreateScope();var gate=locked.ServiceProvider.GetRequiredService<IPoolMutationLock>();var id=Guid.NewGuid();await using var held=await gate.AcquireAsync(id,default);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMilliseconds(200));await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>gate.AcquireAsync(id,timeout.Token));
    }

    [PostgresFact]
    public async Task Financial_category_insufficient_balance_and_forged_items_cannot_pay()
    {
        await using var s=Services();await Migrate(s);var user=Guid.NewGuid();var wallets=await SeedWallets(user,"hotel");var(p,c)=await Checkout(s,user);
        await Assert.ThrowsAnyAsync<Exception>(()=>Send(s,new PayOrderCommand(c.OrderId,user,"User",[new(wallets.Org,p.TotalMinor)],"category")));
        await Assert.ThrowsAnyAsync<Exception>(()=>Send(s,new PayOrderCommand(c.OrderId,user,"User",[new(wallets.Personal,3000000)],"balance")));
        var choices=await Catalogue(s);var quote=await Send(s,new QuotePoolPurchaseQuery(choices));var pending=await Send(s,new CreatePoolPurchaseCommand(user,choices,quote.TotalMinor,Guid.NewGuid().ToString("N")));
        var items=pending.Requests.Select(x=>new CreateOrderItemInput("ووچر",x.TotalMinor,1,0,x.RequestId,"hotel",null,null)).ToList();
        await Assert.ThrowsAsync<PoolException>(()=>Send(s,new CreateOrderCommand(user,"Pools",pending.Id,items,$"pools-purchase:{pending.Id:N}",ReferenceType:"PoolPurchase",SagaId:pending.Id,PayableUntil:pending.PayableUntil)));
        await using var db=new NpgsqlConnection(Connection);foreach(var id in new[]{wallets.Org,wallets.Personal})Assert.Equal(2000000,await db.ExecuteScalarAsync<long>("select available_minor from wallets.wallet_balances where wallet_id=@Id",new{Id=id}));
    }
    [PostgresFact]
    public async Task Concurrent_consumption_and_refund_never_produce_a_refunded_consumed_voucher()
    {
        await using var s=Services();await Migrate(s);var user=Guid.NewGuid();var wallets=await SeedWallets(user);var(p,c)=await Checkout(s,user);
        await Send(s,new PayOrderCommand(c.OrderId,user,"User",[new(wallets.Personal,p.TotalMinor)],"pay"));await Reconcile(s,p.Id);p=await Send(s,new GetPoolPurchaseQuery(p.Id,user));var v=p.Requests[0];var op=Guid.NewGuid();s.GetRequiredService<GrantState>().User=op;s.GetRequiredService<TestClock>().Now=v.StartsAtUtc;
        async Task Consume(){try{await Send(s,new RedeemPoolVoucherCommand(op,v.PoolId,v.Code!,"race"));}catch(PoolException){}}
        async Task Cancel(){try{await Send(s,new CancelPoolPurchaseCommand(p.Id,user));}catch(Refahi.Modules.Orders.Domain.Exceptions.OrderStateConflictException){}}
        await Task.WhenAll(Consume(),Cancel()).WaitAsync(TimeSpan.FromSeconds(20));await Reconcile(s,p.Id);
        var final=await Send(s,new GetPoolPurchaseQuery(p.Id,user));Assert.False(final.Status=="Refunded"&&final.Requests.Any(x=>x.Status=="Redeemed"));
        await using var db=new NpgsqlConnection(Connection);Assert.Equal(final.Status=="Refunded"?2000000:500000,await db.ExecuteScalarAsync<long>("select available_minor from wallets.wallet_balances where wallet_id=@Id",new{Id=wallets.Personal}));
    }
    [PostgresFact]
    public async Task Keyset_recovery_pages_and_database_uniqueness_are_real_postgres_constraints()
    {
        await using var s=Services();await Migrate(s);using var scope=s.CreateScope();var db=scope.ServiceProvider.GetRequiredService<PoolsDbContext>();var now=DateTimeOffset.UtcNow;
        PoolPurchase New(Guid user,string key)=>PoolPurchase.Create(user,key,"hash",[PoolRequest.Create(Guid.NewGuid(),Guid.NewGuid(),"poolticket","12","9","{}","استخر","date","time",1,0,100,now.AddDays(1),now.AddDays(1).AddHours(2))],now,now.AddMinutes(10));
        for(var i=0;i<61;i++)db.Purchases.Add(New(Guid.NewGuid(),Guid.NewGuid().ToString("N")));await db.SaveChangesAsync();
        var repo=scope.ServiceProvider.GetRequiredService<IPoolPurchaseRepository>();var seen=new HashSet<Guid>();DateTimeOffset? after=null;Guid? afterId=null;
        while(true){var ids=await repo.RecoveryIdsAsync(after,afterId,default);if(ids.Count==0)break;Assert.All(ids,id=>Assert.True(seen.Add(id)));var last=(await repo.GetAsync(ids[^1],default))!;after=last.CreatedAt;afterId=last.Id;}Assert.True(seen.Count>=61);
        var user=Guid.NewGuid();db.Purchases.Add(New(user,"unique"));await db.SaveChangesAsync();db.Purchases.Add(New(user,"unique"));await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Http_endpoints_enforce_ownership_validation_envelopes_and_private_purchase_cache()
    {
        await using var services=Services();await Migrate(services);var choices=await Catalogue(services);
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Test"});builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped<IServiceScope>(_=>services.CreateScope());builder.Services.AddScoped(sp=>sp.GetRequiredService<IServiceScope>().ServiceProvider.GetRequiredService<IMediator>());
        builder.Services.AddSingleton(services.GetRequiredService<IOptionsMonitor<PoolsOptions>>());
        builder.Services.AddAuthentication("PoolsTest").AddScheme<AuthenticationSchemeOptions,TestAuthentication>("PoolsTest",_=>{});
        builder.Services.AddAuthorization(o=>o.AddPolicy("UserOrAdmin",p=>p.RequireAuthenticatedUser().RequireRole("User","Admin")));
        await using var app=builder.Build();app.UseAuthentication();app.UseAuthorization();var routes=app.MapGroup("/api/pools");new Refahi.Modules.Pools.Api.Endpoints.PoolCatalogEndpoints().Map(routes);new Refahi.Modules.Pools.Api.Endpoints.PoolPurchaseEndpoints().Map(routes);await app.StartAsync();
        using var client=new HttpClient{BaseAddress=new(app.Urls.Single())};Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/pools/catalog/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/pools/catalog/sessions?gender=9")).StatusCode);
        var request=new{Choices=choices,ExpectedTotalMinor=1500000,IdempotencyKey=Guid.NewGuid().ToString("N")};Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/pools/purchases",request)).StatusCode);
        var user=Guid.NewGuid();client.DefaultRequestHeaders.Add("X-Test-User",user.ToString());var response=await client.PostAsJsonAsync("/api/pools/purchases",request);Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.True(response.Headers.CacheControl!.NoStore);Assert.True(response.Headers.CacheControl.Private);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());var id=json.RootElement.GetProperty("data").GetProperty("id").GetGuid();Assert.True(json.RootElement.GetProperty("success").GetBoolean());Assert.DoesNotContain("providerKey",await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Remove("X-Test-User");client.DefaultRequestHeaders.Add("X-Test-User",Guid.NewGuid().ToString());Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/api/pools/purchases/{id}")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-User");client.DefaultRequestHeaders.Add("X-Test-User",user.ToString());var pdf=await client.GetAsync($"/api/pools/purchases/{id}/vouchers.pdf");Assert.Equal(HttpStatusCode.Conflict,pdf.StatusCode);Assert.Equal("application/json",pdf.Content.Headers.ContentType!.MediaType);Assert.True(pdf.Headers.CacheControl!.NoStore);await app.StopAsync();
    }
    public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if(!Guid.TryParse(Request.Headers["X-Test-User"],out var user))return Task.FromResult(AuthenticateResult.NoResult());
            var principal=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,user.ToString()),new(ClaimTypes.Role,"User")],Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal,Scheme.Name)));
        }
    }

    [PostgresFact]
    public async Task Multi_page_sync_and_partial_failure_preserve_complete_old_projection()
    {
        await using var services=Services();await Migrate(services);await Catalogue(services);var provider=services.GetRequiredService<TestPoolProvider>();provider.MultiPage=true;provider.FailPageTwo=true;
        async Task Sync(bool venues){using var scope=services.CreateScope();await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SyncPoolCatalogCommand(venues));}
        await Assert.ThrowsAsync<PoolException>(()=>Sync(true));Assert.Equal(1,(await Send(services,new ListPoolVenuesQuery(new()))).Total);
        provider.FailPageTwo=false;await Sync(true);var page=await Send(services,new ListPoolVenuesQuery(new(Limit:10)));Assert.Equal(51,page.Total);Assert.Equal(10,page.Items.Count);var first=page.Items.Select(x=>x.Id).ToArray();await Sync(true);Assert.Equal(first,(await Send(services,new ListPoolVenuesQuery(new(Limit:10)))).Items.Select(x=>x.Id));
        provider.FailPoolId="101";await Sync(false);Assert.Equal(100,(await Send(services,new ListPoolSessionsQuery(new()))).Total);
    }

    public sealed class RefundFault { public bool Fail; }
    [PostgresFact]
    public async Task Refund_failure_keeps_codes_revoked_and_recovery_retries_original_allocations()
    {
        await using var s=Services();await Migrate(s);var user=Guid.NewGuid();var wallets=await SeedWallets(user);var(p,c)=await Checkout(s,user);
        using(var scope=s.CreateScope())await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new IssuePoolVouchersCommand(p.Id));
        Assert.All((await Send(s,new GetPoolPurchaseQuery(p.Id,user))).Requests,x=>Assert.Null(x.Code));
        await Send(s,new PayOrderCommand(c.OrderId,user,"User",[new(wallets.Org,600000),new(wallets.Personal,900000)],"pay"));await Reconcile(s,p.Id);
        s.GetRequiredService<RefundFault>().Fail=true;await Assert.ThrowsAsync<PoolException>(()=>Send(s,new CancelPoolPurchaseCommand(p.Id,user)));
        var pending=await Send(s,new GetPoolPurchaseQuery(p.Id,user));Assert.Equal("CancellationPending",pending.Status);Assert.All(pending.Requests,x=>{Assert.Null(x.Code);Assert.Equal("Revoked",x.Status);});
        s.GetRequiredService<RefundFault>().Fail=false;await Reconcile(s,p.Id);Assert.Equal("Refunded",(await Send(s,new GetPoolPurchaseQuery(p.Id,user))).Status);
        await using var db=new NpgsqlConnection(Connection);foreach(var id in new[]{wallets.Org,wallets.Personal})Assert.Equal(2000000,await db.ExecuteScalarAsync<long>("select available_minor from wallets.wallet_balances where wallet_id=@Id",new{Id=id}));
    }
    public sealed class ValidationPipeline<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators, RefundFault faults) : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        {

            if (request is Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment.RefundPaymentCommand && faults.Fail) throw new PoolException("خطای استرداد آزمایشی");
            foreach (var validator in validators) await validator.ValidateAndThrowAsync(request, ct);
            return await next();
        }
    }
}


