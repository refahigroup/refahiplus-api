using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Domain.Events;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using System.Text.Json;

namespace Refahi.Modules.Store.Application.Features.Checkout.FinalizeStoreOrder;

public sealed class StoreOrderPaidEventHandler : INotificationHandler<OrderPaidEvent>
{
    private readonly ICartRepository _cartRepository;
    private readonly IProductRepository _productRepository;
    private readonly IProductSessionRepository _sessionRepository;
    private readonly IMediator _mediator;

    public StoreOrderPaidEventHandler(
        ICartRepository cartRepository,
        IProductRepository productRepository,
        IProductSessionRepository sessionRepository,
        IMediator mediator)
    {
        _cartRepository = cartRepository;
        _productRepository = productRepository;
        _sessionRepository = sessionRepository;
        _mediator = mediator;
    }

    public async Task Handle(OrderPaidEvent notification, CancellationToken cancellationToken)
    {
        var order = await _mediator.Send(
            new GetOrderByIdQuery(notification.OrderId, notification.UserId, "Admin"),
            cancellationToken);

        if (order is null || !order.SourceModule.Equals("Store", StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var item in order.Items)
        {
            var metadata = ReadMetadata(item.MetadataJson);
            var variantId = ReadGuid(metadata, "variant_id");
            var sessionId = ReadGuid(metadata, "session_id");

            if (sessionId.HasValue)
            {
                var session = await _sessionRepository.GetByIdAsync(sessionId.Value, cancellationToken);
                if (session is not null)
                {
                    session.Sell(item.Quantity);
                    await _sessionRepository.UpdateAsync(session, cancellationToken);
                }

                continue;
            }

            await DecreaseStockAsync(item.SourceItemId, variantId, item.Quantity, cancellationToken);
        }

        var cart = await _cartRepository.GetByUserIdAsync(order.UserId, cancellationToken);
        if (cart is not null)
        {
            cart.Clear();
            await _cartRepository.UpdateAsync(cart, cancellationToken);
        }
    }

    private async Task DecreaseStockAsync(
        Guid productId,
        Guid? variantId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product is null)
            return;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (variantId.HasValue)
                    product.DecreaseVariantStock(variantId.Value, quantity);
                else
                    product.DecreaseStock(quantity);

                await _productRepository.UpdateAsync(product, cancellationToken);
                return;
            }
            catch (StoreConcurrencyException) when (attempt < 3)
            {
                product = await _productRepository.GetByIdAsync(productId, cancellationToken)
                    ?? throw new StoreDomainException("Product was not found.", "PRODUCT_NOT_FOUND");

                await Task.Delay(50 * attempt, cancellationToken);
            }
        }
    }

    private static JsonElement? ReadMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static Guid? ReadGuid(JsonElement? metadata, string propertyName)
    {
        if (metadata is null ||
            metadata.Value.ValueKind != JsonValueKind.Object ||
            !metadata.Value.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return Guid.TryParse(value.GetString(), out var id) ? id : null;
    }
}
