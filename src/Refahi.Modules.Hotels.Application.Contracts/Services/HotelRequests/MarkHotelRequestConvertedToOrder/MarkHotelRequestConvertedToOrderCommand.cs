using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.MarkHotelRequestConvertedToOrder;

public sealed record MarkHotelRequestConvertedToOrderCommand(
    Guid RequestId,
    Guid UserId,
    Guid OrderId) : IRequest;
