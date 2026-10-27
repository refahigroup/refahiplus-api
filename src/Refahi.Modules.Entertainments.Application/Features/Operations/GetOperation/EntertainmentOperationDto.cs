namespace Refahi.Modules.Entertainments.Application.Features.Operations.GetOperation;

public sealed record EntertainmentOperationDto(
    Guid EntertainmentPurchaseId, 
    Guid FulfillmentId, 
    string ProviderKey, 
    string Status,
    string? FailureReason, 
    DateTimeOffset UpdatedAt, 
    IReadOnlyList<EntertainmentOperationAttemptDto> Attempts
);