namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceCancellationRequest(
    string OperationId, 
    string ProviderOrderCode, 
    int AdultCount, 
    int ChildCount
);
