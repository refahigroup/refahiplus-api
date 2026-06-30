using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Services;

namespace Refahi.Modules.Orders.Application.Features.CreateOrder;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CreateOrderResponse>
{
    private readonly IOrderCreationGateway _gateway;

    public CreateOrderCommandHandler(IOrderCreationGateway gateway)
    {
        _gateway = gateway;
    }

    public async Task<CreateOrderResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        => await _gateway.CreateAsync(request, cancellationToken);
}
