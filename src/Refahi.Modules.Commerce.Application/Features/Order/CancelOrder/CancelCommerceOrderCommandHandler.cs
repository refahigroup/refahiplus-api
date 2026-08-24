using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Commerce.Application.Features.Order.CancelOrder;

public sealed class CancelCommerceOrderCommandHandler(ICommerceRepository repository, ICommerceSecretProtector secrets, IMediator mediator) :
    IRequestHandler<CancelCommerceOrderCommand, CommerceOrderDto>
{
    public async Task<CommerceOrderDto> Handle(CancelCommerceOrderCommand request, CancellationToken ct)
    {
        var value = await repository.GetOrderAsync(request.CommerceOrderId, ct) 
            ?? throw new CommerceDomainException("سفارش Commerce یافت نشد", "ORDER_NOT_FOUND");

        EnsureOwner(value, request.UserId, request.CallerRole); 
        
        if (!value.OrderId.HasValue) 
            throw new CommerceDomainException("Order متصل یافت نشد", "ORDER_NOT_ATTACHED");

        await mediator.Send(new CancelOrderCommand(
            value.OrderId.Value, 
            request.Reason, 
            request.IdempotencyKey,
            CallerUserId: request.UserId, 
            CallerRole: 
            request.CallerRole
        ), ct);

        value = await repository.GetOrderAsync(value.Id, ct) 
            ?? value; return Map(value, false, secrets);

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



