using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.FinalizeHotelBookingAfterPayment;

public sealed record FinalizeHotelBookingAfterPaymentCommand(
    Guid OrderId,
    Guid UserId,
    Guid PaymentId,
    Guid? SagaId = null) : IRequest;
