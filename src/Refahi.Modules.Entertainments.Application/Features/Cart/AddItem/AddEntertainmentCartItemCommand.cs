using MediatR;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.AddItem;

public sealed record AddEntertainmentCartItemCommand(
    Guid UserId, 
    string ProviderKey, 
    string SupplierKey, 
    string ProgramKey,
    string OfferKey, 
    string PurchaseOptionKey, 
    int Quantity, 
    long ExpectedUnitPriceMinor
) : IRequest<EntertainmentCartDto>;

