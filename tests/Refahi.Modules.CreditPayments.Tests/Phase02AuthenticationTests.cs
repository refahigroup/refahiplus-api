using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.CreditPayments.Api;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase02AuthenticationTests
{
    [Fact] public async Task No_authorization_returns_no_result()=>Assert.True((await Authenticate(null,new StubAuthenticator(null))).None);
    [Theory][InlineData("Basic !!!")][InlineData("Basic bm9jb2xvbg==")]
    public async Task Malformed_basic_has_normalized_failure(string header){var r=await Authenticate(header,new StubAuthenticator(null));Assert.False(r.Succeeded);Assert.Equal("اعتبارنامه نامعتبر است",r.Failure!.Message);}
    [Theory][InlineData("unknown","secret")][InlineData("known","wrong")]
    public async Task Unknown_client_and_wrong_secret_have_same_external_failure(string client,string secret){var r=await Authenticate(Basic(client,secret),new StubAuthenticator(null));Assert.Equal("اعتبارنامه نامعتبر است",r.Failure!.Message);}
    [Fact] public async Task Valid_credential_creates_trusted_context(){var expected=new MerchantAuthenticationContext(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),new HashSet<string>{CredentialScopes.PaymentCreate});var r=await Authenticate(Basic("known","secret"),new StubAuthenticator(expected));Assert.True(r.Succeeded);Assert.Equal(expected.MerchantId.ToString(),r.Principal!.FindFirst("merchant_id")!.Value);Assert.Equal(CredentialScopes.PaymentCreate,r.Principal.FindFirst("scope")!.Value);}
    [Fact] public async Task Authenticator_rejects_wrong_revoked_and_disabled_graphs()
    {
        var secrets=new Pbkdf2CredentialSecretService();var material=secrets.Generate();var now=DateTimeOffset.UtcNow;var merchant=Merchant.Create(Guid.NewGuid(),"فروشگاه",now);merchant.Activate(now);var terminal=merchant.AddTerminal("وب","store",1,10,["https://example.com/c"],now);var credential=terminal.AddCredential(material.ClientId,material.Hash,material.Algorithm,material.Version,[CredentialScopes.PaymentRead],"اصلی",now.AddHours(1),now);var repo=new GraphRepository(credential,terminal,merchant);var auth=new MerchantCredentialAuthenticator(repo,secrets);
        Assert.Null(await auth.AuthenticateAsync(material.ClientId,"wrong",default));Assert.NotNull(await auth.AuthenticateAsync(material.ClientId,material.Secret,default));credential.Revoke(now);Assert.Null(await auth.AuthenticateAsync(material.ClientId,material.Secret,default));
    }
    [Fact] public async Task Suspended_merchant_and_terminal_authenticate_for_operation_specific_authorization_but_disabled_do_not()
    {
        var secrets=new Pbkdf2CredentialSecretService();var material=secrets.Generate();var now=DateTimeOffset.UtcNow;
        var merchant=Merchant.Create(Guid.NewGuid(),"فروشگاه",now);merchant.Activate(now);var terminal=merchant.AddTerminal("وب","store",1,10,["https://example.com/c"],now);
        var credential=terminal.AddCredential(material.ClientId,material.Hash,material.Algorithm,material.Version,[CredentialScopes.RefundCreate,CredentialScopes.RefundRead],"اصلی",now.AddHours(1),now);
        terminal.Suspend(now);merchant.Suspend(now);var auth=new MerchantCredentialAuthenticator(new GraphRepository(credential,terminal,merchant),secrets);
        var suspended=await auth.AuthenticateAsync(material.ClientId,material.Secret,default);Assert.NotNull(suspended);Assert.Equal("suspended",suspended!.MerchantStatus);Assert.Equal("suspended",suspended.TerminalStatus);
        merchant.Disable(now);Assert.Null(await auth.AuthenticateAsync(material.ClientId,material.Secret,default));
    }
    private static async Task<AuthenticateResult> Authenticate(string? header,IMerchantCredentialAuthenticator authenticator){var s=new ServiceCollection();s.AddLogging();s.AddSingleton(authenticator);s.AddAuthentication().AddScheme<AuthenticationSchemeOptions,MerchantBasicAuthenticationHandler>(MerchantBasicAuthenticationHandler.SchemeName,_=>{});var provider=s.BuildServiceProvider();var context=new DefaultHttpContext{RequestServices=provider};if(header is not null)context.Request.Headers.Authorization=header;return await context.AuthenticateAsync(MerchantBasicAuthenticationHandler.SchemeName);}
    private static string Basic(string id,string secret)=>"Basic "+Convert.ToBase64String(Encoding.UTF8.GetBytes(id+":"+secret));
    private sealed class StubAuthenticator(MerchantAuthenticationContext? value):IMerchantCredentialAuthenticator { public Task<MerchantAuthenticationContext?> AuthenticateAsync(string clientId,string secret,CancellationToken ct)=>Task.FromResult(value); }
    private sealed class GraphRepository(TerminalCredential c,Terminal t,Merchant m):ICreditPaymentsRepository
    {
        public Task<(TerminalCredential Credential,Terminal Terminal,Merchant Merchant)?> GetAuthenticationGraphAsync(string id,CancellationToken ct)=>Task.FromResult(id==c.ClientId?((TerminalCredential,Terminal,Merchant)?)(c,t,m):null);
        public Task<Merchant?> GetMerchantAsync(Guid id,bool tracking,CancellationToken ct)=>Task.FromResult<Merchant?>(null);public Task<Merchant?> GetMerchantBySupplierAsync(Guid id,CancellationToken ct)=>Task.FromResult<Merchant?>(null);public Task<Terminal?> GetTerminalAsync(Guid id,bool tracking,CancellationToken ct)=>Task.FromResult<Terminal?>(null);public Task<TerminalCredential?> GetCredentialAsync(Guid id,bool tracking,CancellationToken ct)=>Task.FromResult<TerminalCredential?>(null);public void Add(Merchant x){}public void AddAudit(AuditRecord x){}public Task SaveChangesAsync(CancellationToken ct)=>Task.CompletedTask;
    }
}
