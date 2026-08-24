namespace Refahi.Modules.Commerce.Application.Features.Operations.GetOperation;

public sealed record CommerceOperationAttemptDto(
    Guid Id,
    string Operation, 
    string IdempotencyKey, 
    string PayloadHash,
    string Outcome, 
    string? SanitizedError, 
    DateTimeOffset StartedAt, 
    DateTimeOffset? CompletedAt
);

