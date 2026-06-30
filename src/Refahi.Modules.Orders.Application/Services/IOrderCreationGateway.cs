using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Orders.Application.Services;

public interface IOrderCreationGateway
{
    Task<CreateOrderResponse> CreateAsync(CreateOrderCommand request, CancellationToken cancellationToken);
}
