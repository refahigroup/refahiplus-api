namespace Refahi.Modules.CreditPayments.Application;

public sealed record MerchantAuthenticationContext(Guid MerchantId, Guid TerminalId, Guid CredentialId,
    IReadOnlySet<string> Scopes, string MerchantStatus = "active", string TerminalStatus = "active");
public interface IMerchantCredentialAuthenticator
{
    Task<MerchantAuthenticationContext?> AuthenticateAsync(string clientId, string secret, CancellationToken ct);
    async Task<MerchantAuthenticationResult> AuthenticateDetailedAsync(string clientId,string secret,CancellationToken ct)
    {
        var context=await AuthenticateAsync(clientId,secret,ct);
        return new(context,context is null?"merchant_auth_failed":null);
    }
}

public sealed record MerchantAuthenticationResult(MerchantAuthenticationContext? Context,string? FailureCategory,
    Guid? CredentialId=null,Guid? TerminalId=null);
