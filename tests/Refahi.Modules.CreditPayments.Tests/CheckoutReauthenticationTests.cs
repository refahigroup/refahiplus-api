using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class CheckoutReauthenticationTests
{
    [Fact]
    public void Reauthentication_preserves_awaiting_funding_and_replaces_only_same_customer_secret()
    {
        var now = new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
        var customerId = Guid.NewGuid();
        var session = CheckoutSession.Create(Guid.NewGuid(), Guid.NewGuid(), new byte[32],
            Enumerable.Repeat((byte)1, 32).ToArray(), 1, now.AddMinutes(15), now);
        session.Authenticate(customerId, Enumerable.Repeat((byte)2, 32).ToArray(), now.AddMinutes(15), now);
        session.MarkAwaitingFunding(now.AddMinutes(1));

        Assert.Throws<CreditPaymentsDomainException>(() => session.Authenticate(Guid.NewGuid(),
            Enumerable.Repeat((byte)3, 32).ToArray(), now.AddMinutes(15), now.AddMinutes(2)));
        session.Authenticate(customerId, Enumerable.Repeat((byte)4, 32).ToArray(),
            now.AddMinutes(15), now.AddMinutes(2));

        Assert.Equal(CheckoutSessionStatus.AwaitingFunding, session.Status);
        Assert.Equal(customerId, session.CustomerId);
        Assert.Equal(Enumerable.Repeat((byte)4, 32), session.AuthorizationSecretHash);
    }
}
