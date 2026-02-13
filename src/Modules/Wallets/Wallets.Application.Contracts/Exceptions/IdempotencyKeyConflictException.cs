using Wallets.Application.Contracts.Exceptions.Abstraction;

namespace Wallets.Application.Contracts.Exceptions;

public sealed class IdempotencyKeyConflictException : WalletApplicationException
{
    public IdempotencyKeyConflictException()
        : base("IDEMPOTENCY_KEY_CONFLICT", "The idempotency key was reused with a different request payload.") { }
}
