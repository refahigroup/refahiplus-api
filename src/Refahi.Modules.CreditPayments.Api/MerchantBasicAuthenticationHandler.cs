using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.CreditPayments.Api;

public sealed class MerchantBasicAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,IMerchantCredentialAuthenticator authenticator):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    public const string SchemeName="CreditPaymentsBasic";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if(!Request.Headers.TryGetValue("Authorization",out var header))return AuthenticateResult.NoResult();
        var value=header.ToString(); if(!value.StartsWith("Basic ",StringComparison.OrdinalIgnoreCase))return AuthenticateResult.Fail("اعتبارنامه نامعتبر است");
        try{var decoded=Encoding.UTF8.GetString(Convert.FromBase64String(value[6..].Trim()));var separator=decoded.IndexOf(':');if(separator<=0)return AuthenticateResult.Fail("اعتبارنامه نامعتبر است");var result=await authenticator.AuthenticateDetailedAsync(decoded[..separator],decoded[(separator+1)..],Context.RequestAborted);var context=result.Context;if(context is null)return AuthenticateResult.Fail("اعتبارنامه نامعتبر است");var claims=new List<Claim>{new("merchant_id",context.MerchantId.ToString()),new("terminal_id",context.TerminalId.ToString()),new("credential_id",context.CredentialId.ToString()),new("merchant_status",context.MerchantStatus),new("terminal_status",context.TerminalStatus)};claims.AddRange(context.Scopes.Select(x=>new Claim("scope",x)));var principal=new ClaimsPrincipal(new ClaimsIdentity(claims,SchemeName));return AuthenticateResult.Success(new AuthenticationTicket(principal,SchemeName));}catch(FormatException){return AuthenticateResult.Fail("اعتبارنامه نامعتبر است");}
    }
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode=StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(ApiResponseHelper.Error("اعتبارنامه نامعتبر است",
            new Dictionary<string,string[]>{{"code",["authentication_error"]}},401));
    }
    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode=StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(ApiResponseHelper.Error("دسترسی کافی نیست",
            new Dictionary<string,string[]>{{"code",["authorization_error"]}},403));
    }
}
