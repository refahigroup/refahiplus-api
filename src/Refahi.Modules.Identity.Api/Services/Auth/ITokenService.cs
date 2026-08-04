namespace Refahi.Modules.Identity.Api.Services.Auth;

public interface ITokenService
{
    Task<TokenResult> CreateTokensAsync(UserIdentity user, CancellationToken ct = default);
}

