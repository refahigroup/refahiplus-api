namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceFulfillmentRequest(
    string OperationId, 
    string RecipientName, 
    string RecipientMobile,
    IReadOnlyList<CommerceFulfillmentLine> Lines
);
