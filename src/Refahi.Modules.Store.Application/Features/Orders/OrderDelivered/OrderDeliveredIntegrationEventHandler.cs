using MediatR;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Orders.OrderDelivered;

public class OrderDeliveredIntegrationEventHandler : INotificationHandler<OrderDeliveredIntegrationEvent>
{
    private readonly IShopRepository _shopRepository;

    public OrderDeliveredIntegrationEventHandler(IShopRepository shopRepository)
    {
        _shopRepository = shopRepository;
    }

    public async Task Handle(OrderDeliveredIntegrationEvent notification, CancellationToken cancellationToken)
    {
        // فقط سفارش‌های ماژول Store را پردازش می‌کنیم
        if (!string.Equals(notification.SourceModule, "Store", StringComparison.OrdinalIgnoreCase))
            return;

        var shop = await _shopRepository.GetByIdAsync(notification.SourceReferenceId, cancellationToken);
        if (shop is null) return;

        shop.RecordDelivery();
        await _shopRepository.UpdateAsync(shop, cancellationToken);
    }
}
