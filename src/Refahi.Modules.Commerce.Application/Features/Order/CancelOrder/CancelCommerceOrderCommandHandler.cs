using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Commerce.Application.Features.Order.CancelOrder;

public sealed class CancelCommerceOrderCommandHandler(ICommerceRepository repository, ICommerceSecretProtector secrets, IMediator mediator, Refahi.Modules.Commerce.Application.Contracts.Providers.ICommerceProviderFactory providers) :
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
            ?? value; return CommerceOrderMapper.Map(value, false, secrets, providers);

    }

    internal static void EnsureOwner(CommerceOrder value, Guid userId, string role)
    { 
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && value.UserId != userId) 
            throw new UnauthorizedAccessException("دسترسی به این سفارش مجاز نیست"); 
    }

}
