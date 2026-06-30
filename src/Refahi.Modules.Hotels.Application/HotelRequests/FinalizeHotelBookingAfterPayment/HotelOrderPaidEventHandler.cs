using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.FinalizeHotelBookingAfterPayment;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.Hotels.Application.HotelRequests.FinalizeHotelBookingAfterPayment;

public sealed class HotelOrderPaidEventHandler : INotificationHandler<OrderPaidIntegrationEvent>
{
    private readonly IMediator _mediator;

    public HotelOrderPaidEventHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Handle(OrderPaidIntegrationEvent notification, CancellationToken cancellationToken)
    {
        if (!notification.SourceModule.Equals("Hotel", StringComparison.OrdinalIgnoreCase) ||
            !notification.ReferenceType.Equals("HotelRequest", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _mediator.Send(new FinalizeHotelBookingAfterPaymentCommand(
            notification.OrderId,
            notification.UserId,
            notification.PaymentId,
            notification.SagaId), cancellationToken);
    }
}
