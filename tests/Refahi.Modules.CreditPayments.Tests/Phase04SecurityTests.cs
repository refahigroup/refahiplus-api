using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase04SecurityTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Checkout_token_is_stable_url_safe_high_entropy_and_not_stored_raw()
    {
        var service = Security();
        var sessionId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var first = service.CreateCheckoutToken(sessionId, paymentId);
        var second = service.CreateCheckoutToken(Guid.NewGuid(), Guid.NewGuid());
        var session = CheckoutSession.Create(sessionId, paymentId, first.Nonce, first.TokenHash,
            first.KeyVersion, Now.AddMinutes(15), Now);

        Assert.Equal(43, first.Token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", first.Token);
        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(first.Token, service.RecreateCheckoutToken(session));
        Assert.DoesNotContain(first.Token, Convert.ToBase64String(first.Nonce));
        Assert.DoesNotContain(first.Token, Convert.ToBase64String(first.TokenHash));
        Assert.True(service.TryHashPresentedToken(first.Token, out var hash));
        Assert.Equal(first.TokenHash, hash);
        // Mutating the final Base64URL character can change only unused padding
        // bits and decode to the same bytes. Change a fully significant character
        // so this is a deterministic token-integrity test.
        var mutated = (first.Token[0] == 'A' ? "B" : "A") + first.Token[1..];
        Assert.True(service.TryHashPresentedToken(mutated, out var mutatedHash));
        Assert.NotEqual(first.TokenHash, mutatedHash);
        Assert.False(service.TryHashPresentedToken("not+a+token", out _));
    }

    [Fact]
    public void Customer_and_checkout_authorization_are_immutable_and_secret_is_hash_only()
    {
        var service = Security();
        var token = service.CreateCheckoutToken(Guid.NewGuid(), Guid.NewGuid());
        var session = CheckoutSession.Create(Guid.NewGuid(), Guid.NewGuid(), token.Nonce, token.TokenHash,
            token.KeyVersion, Now.AddMinutes(15), Now);
        var authorization = service.CreateAuthorizationSecret();
        var customer = Guid.NewGuid();

        session.Authenticate(customer, authorization.SecretHash, Now.AddMinutes(15), Now.AddMinutes(1));

        Assert.Equal(CheckoutSessionStatus.Authenticated, session.Status);
        Assert.Equal(customer, session.CustomerId);
        Assert.True(service.VerifyAuthorizationSecret(authorization.Secret, session.AuthorizationSecretHash!));
        Assert.DoesNotContain(authorization.Secret, Convert.ToBase64String(session.AuthorizationSecretHash!));
        Assert.Throws<CreditPaymentsDomainException>(() => session.Authenticate(
            Guid.NewGuid(), service.CreateAuthorizationSecret().SecretHash, Now.AddMinutes(15), Now.AddMinutes(2)));
    }

    private static HmacCheckoutSecurityService Security() => new(Options.Create(new CheckoutOptions
    {
        PublicBaseUrl = "https://pay.example",
        ActiveTokenKeyVersion = 7,
        TokenKeys = new() { [7] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(x => (byte)x).ToArray()) }
    }));
}
