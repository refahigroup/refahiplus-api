using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Order.CancelOrder;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Commerce.Application.Features.Order.GetOrder;

public sealed class GetCommerceOrderQueryHandler(ICommerceRepository repository, ICommerceSecretProtector secrets, IMediator mediator) :
    IRequestHandler<GetCommerceOrderQuery, CommerceOrderDto?>
{
    public async Task<CommerceOrderDto?> Handle(GetCommerceOrderQuery request, CancellationToken ct)
    { 
        var value = await repository.GetOrderAsync(request.CommerceOrderId, ct); 

        if (value is null) 
            return null; 
        
        EnsureOwner(value, request.UserId, request.CallerRole); 
        
        return Map(value, request.RevealTickets, secrets); 
    }



    internal static void EnsureOwner(CommerceOrder value, Guid userId, string role)
    { 
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && value.UserId != userId) 
            throw new UnauthorizedAccessException("دسترسی به این سفارش مجاز نیست"); 
    }

    internal static CommerceOrderDto Map(CommerceOrder x, bool reveal, ICommerceSecretProtector secrets) => 
        new( 
            x.Id, 
            x.OrderId,
            x.Status.ToString(), 
            x.TotalAmountMinor,
            x.Items.Select(i => new CommerceOrderItemDto(
                i.Id, 
                i.ProviderKey, 
                i.SellerKey,
                i.Title, 
                i.OfferTitle, 
                i.PurchaseOptionKey, 
                i.Quantity, 
                i.UnitPriceMinor, 
                i.CategoryCode)
            ).ToArray(),
            x.Fulfillments.Select(f => new CommerceFulfillmentDto(
                f.Id, 
                f.ProviderKey, 
                f.Status.ToString(), 
                f.ProviderOrderCode, 
                f.FailureReason,
                f.Tickets.Select(t => new CommerceTicketDto(
                    t.Id, 
                    t.IsChild, 
                    reveal ? secrets.Unprotect(t.CodeProtected) : null)
                ).ToArray()
            )).ToArray()
        );
}
