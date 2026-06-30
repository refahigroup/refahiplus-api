using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.ConvertHotelRequestToOrder;

public sealed record ConvertHotelRequestToOrderCommand(
    Guid RequestId,
    Guid UserId,
    string IdempotencyKey) : IRequest<ConvertHotelRequestToOrderResponse>;

public sealed record ConvertHotelRequestToOrderResponse(
    Guid RequestId,
    Guid OrderId,
    string? OrderNumber,
    long FinalAmountMinor,
    string Status);
