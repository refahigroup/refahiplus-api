namespace Identity.Api.Services.Auth;

public interface ITokenService
{
    TokenResult CreateTokens(UserIdentity user);
}

