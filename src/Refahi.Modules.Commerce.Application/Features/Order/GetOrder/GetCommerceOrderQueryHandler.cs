using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Order.GetOrder;

public sealed class GetCommerceOrderQueryHandler(ICommerceRepository repository, ICommerceSecretProtector secrets,
    Refahi.Modules.Commerce.Application.Contracts.Providers.ICommerceProviderFactory providers) :
    IRequestHandler<GetCommerceOrderQuery, CommerceOrderDto?>
{
    public async Task<CommerceOrderDto?> Handle(GetCommerceOrderQuery request, CancellationToken ct)
    { 
        var value = await repository.GetOrderAsync(request.CommerceOrderId, ct); 

        if (value is null) 
            return null; 
        
        EnsureOwner(value, request.UserId, request.CallerRole); 
        
        return CommerceOrderMapper.Map(value, request.RevealTickets, secrets, providers);
    }



    internal static void EnsureOwner(CommerceOrder value, Guid userId, string role)
    { 
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && value.UserId != userId) 
            throw new UnauthorizedAccessException("دسترسی به این سفارش مجاز نیست"); 
    }

}
