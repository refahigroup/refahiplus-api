using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Entertainments.Application.Features.Order.CancelOrder;

public sealed class CancelEntertainmentPurchaseCommandHandler(IEntertainmentRepository repository, IEntertainmentSecretProtector secrets, IMediator mediator, Refahi.Modules.Entertainments.Application.Contracts.Providers.IEntertainmentProviderFactory providers) :
    IRequestHandler<CancelEntertainmentPurchaseCommand, EntertainmentPurchaseDto>
{
    public async Task<EntertainmentPurchaseDto> Handle(CancelEntertainmentPurchaseCommand request, CancellationToken ct)
    {
        var value = await repository.GetOrderAsync(request.EntertainmentPurchaseId, ct) 
            ?? throw new EntertainmentDomainException("سفارش Entertainment یافت نشد", "ORDER_NOT_FOUND");

        EnsureOwner(value, request.UserId, request.CallerRole); 
        
        if (!value.OrderId.HasValue) 
            throw new EntertainmentDomainException("Order متصل یافت نشد", "ORDER_NOT_ATTACHED");

        await mediator.Send(new CancelOrderCommand(
            value.OrderId.Value, 
            request.Reason, 
            request.IdempotencyKey,
            CallerUserId: request.UserId, 
            CallerRole: 
            request.CallerRole
        ), ct);

        value = await repository.GetOrderAsync(value.Id, ct) 
            ?? value; return EntertainmentPurchaseMapper.Map(value, false, secrets, providers);

    }

    internal static void EnsureOwner(EntertainmentPurchase value, Guid userId, string role)
    { 
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && value.UserId != userId) 
            throw new UnauthorizedAccessException("دسترسی به این سفارش مجاز نیست"); 
    }

}
