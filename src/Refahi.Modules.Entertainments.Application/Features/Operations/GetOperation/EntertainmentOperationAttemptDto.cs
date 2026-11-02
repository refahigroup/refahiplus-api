namespace Refahi.Modules.Entertainments.Application.Features.Operations.GetOperation;

public sealed record EntertainmentOperationAttemptDto(
    Guid Id,
    string Operation, 
    string IdempotencyKey, 
    string PayloadHash,
    string Outcome, 
    string? SanitizedError, 
    DateTimeOffset StartedAt, 
    DateTimeOffset? CompletedAt
);

