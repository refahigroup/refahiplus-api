namespace Refahi.Modules.Commerce.Application.Features.Operations.GetOperation;

public sealed record CommerceOperationDto(
    Guid CommerceOrderId, 
    Guid FulfillmentId, 
    string ProviderKey, 
    string Status,
    string? FailureReason, 
    DateTimeOffset UpdatedAt, 
    IReadOnlyList<CommerceOperationAttemptDto> Attempts
);