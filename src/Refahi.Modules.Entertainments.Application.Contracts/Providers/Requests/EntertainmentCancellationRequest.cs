namespace Refahi.Modules.Entertainments.Application.Contracts.Providers.Requests;

public sealed record EntertainmentCancellationRequest(
    string OperationId, 
    string ProviderOrderCode, 
    int AdultCount, 
    int ChildCount
);
