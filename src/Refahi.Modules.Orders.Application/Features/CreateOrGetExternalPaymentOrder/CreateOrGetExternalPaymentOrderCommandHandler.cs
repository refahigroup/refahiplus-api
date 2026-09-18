using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Services;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.CreateOrGetExternalPaymentOrder;

public sealed class CreateOrGetExternalPaymentOrderCommandHandler(
    IOrderCreationGateway creationGateway,
    IOrderRepository orders,
    ILogger<CreateOrGetExternalPaymentOrderCommandHandler> logger)
    : IRequestHandler<CreateOrGetExternalPaymentOrderCommand, ExternalPaymentOrderDto>
{
    public async Task<ExternalPaymentOrderDto> Handle(
        CreateOrGetExternalPaymentOrderCommand request,
        CancellationToken cancellationToken)
    {
        var referenceType = request.SourceModule.Trim();
        var existing = await ResolveExistingAsync(request, referenceType, cancellationToken);
        if (existing is not null)
            return MapAndValidate(existing, request, referenceType, false);

        try
        {
            await creationGateway.CreateAsync(
                new CreateOrderCommand(
                    request.UserId,
                    request.SourceModule,
                    request.SourceReferenceId,
                    [new CreateOrderItemInput(
                        request.Title,
                        request.AmountMinor,
                        1,
                        0,
                        null,
                        request.CategoryCode,
                        null,
                        null)],
                    request.IdempotencyKey,
                    ReferenceType: referenceType,
                    PayableUntil: request.PayableUntil,
                    SourceOwnerId: request.SourceOwnerId),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The create outcome is unknown. Resolve through durable source identity;
            // never issue an unprotected second create.
            existing = await ResolveExistingAsync(request, referenceType, cancellationToken);
            if (existing is null)
                throw;

            logger.LogWarning(
                exception,
                "Recovered an external payment Order after an unknown create outcome. OrderId={OrderId} SourceModule={SourceModule} SourceReferenceId={SourceReferenceId}",
                existing.Id,
                request.SourceModule,
                request.SourceReferenceId);
            return MapAndValidate(existing, request, referenceType, false);
        }

        existing = await ResolveExistingAsync(request, referenceType, cancellationToken)
            ?? throw new InvalidOperationException("سفارش ایجاد شد اما بازیابی آن ممکن نیست");
        return MapAndValidate(existing, request, referenceType, true);
    }

    private async Task<Order?> ResolveExistingAsync(
        CreateOrGetExternalPaymentOrderCommand request,
        string referenceType,
        CancellationToken cancellationToken)
    {
        var bySource = await orders.GetBySourceIdentityWithItemsAsync(
            referenceType,
            request.SourceReferenceId,
            cancellationToken);
        if (bySource is not null)
            return bySource;

        return await orders.GetByIdempotencyKeyWithItemsAsync(
            request.IdempotencyKey,
            cancellationToken);
    }

    private static ExternalPaymentOrderDto MapAndValidate(
        Order order,
        CreateOrGetExternalPaymentOrderCommand request,
        string referenceType,
        bool wasCreated)
    {
        var categoryCode = order.Items.Count == 1 ? order.Items[0].CategoryCode : string.Empty;
        if (order.UserId != request.UserId
            || !string.Equals(order.SourceModule, request.SourceModule, StringComparison.Ordinal)
            || order.SourceReferenceId != request.SourceReferenceId
            || !string.Equals(order.ReferenceType, referenceType, StringComparison.Ordinal)
            || order.SourceOwnerId != request.SourceOwnerId
            || order.FinalAmountMinor != request.AmountMinor
            || !string.Equals(order.Currency, request.Currency, StringComparison.Ordinal)
            || order.Items.Count != 1
            || !string.Equals(categoryCode, request.CategoryCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("هویت یا محتوای سفارش موجود با درخواست مطابقت ندارد");
        }

        return new ExternalPaymentOrderDto(
            order.Id,
            order.UserId,
            order.SourceModule,
            order.SourceReferenceId.Value,
            order.ReferenceType,
            order.SourceOwnerId,
            order.FinalAmountMinor,
            order.Currency,
            categoryCode,
            order.PaymentState.ToString(),
            order.PayableUntil,
            wasCreated);
    }
}
