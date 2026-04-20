using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.HasUserPurchased;

public class HasUserPurchasedQueryHandler : IRequestHandler<HasUserPurchasedQuery, bool>
{
    private readonly IOrderRepository _orderRepository;

    public HasUserPurchasedQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<bool> Handle(HasUserPurchasedQuery request, CancellationToken cancellationToken)
    {
        // Use a generous page size — review check doesn't need full pagination
        var orders = await _orderRepository.GetBySourceAsync(
            request.SourceModule, request.SourceReferenceId, 1, 500, cancellationToken);

        return orders.Any(o =>
            o.UserId == request.UserId &&
            o.Status == OrderStatus.Delivered);
    }
}
