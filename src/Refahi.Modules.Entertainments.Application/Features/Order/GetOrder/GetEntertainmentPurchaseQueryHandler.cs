using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Features.Order.GetOrder;

public sealed class GetEntertainmentPurchaseQueryHandler(IEntertainmentRepository repository, IEntertainmentSecretProtector secrets,
    Refahi.Modules.Entertainments.Application.Contracts.Providers.IEntertainmentProviderFactory providers) :
    IRequestHandler<GetEntertainmentPurchaseQuery, EntertainmentPurchaseDto?>
{
    public async Task<EntertainmentPurchaseDto?> Handle(GetEntertainmentPurchaseQuery request, CancellationToken ct)
    { 
        var value = await repository.GetOrderAsync(request.EntertainmentPurchaseId, ct); 

        if (value is null) 
            return null; 
        
        EnsureOwner(value, request.UserId, request.CallerRole); 
        
        return EntertainmentPurchaseMapper.Map(value, request.RevealTickets, secrets, providers);
    }



    internal static void EnsureOwner(EntertainmentPurchase value, Guid userId, string role)
    { 
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && value.UserId != userId) 
            throw new UnauthorizedAccessException("دسترسی به این سفارش مجاز نیست"); 
    }

}
