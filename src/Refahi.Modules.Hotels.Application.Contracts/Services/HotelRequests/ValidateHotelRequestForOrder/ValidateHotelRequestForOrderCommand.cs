using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.ValidateHotelRequestForOrder;

public sealed record ValidateHotelRequestForOrderCommand(
    Guid RequestId,
    Guid UserId) : IRequest<ValidateHotelRequestForOrderResponse>;

public sealed record ValidateHotelRequestForOrderResponse(
    Guid RequestId,
    Guid UserId,
    long TotalPrice,
    string Currency,
    string Status,
    DateTime ExpireAt);
