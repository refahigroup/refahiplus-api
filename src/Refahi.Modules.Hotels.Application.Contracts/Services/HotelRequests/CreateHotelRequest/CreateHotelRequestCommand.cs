using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest;

public sealed record CreateHotelRequestCommand(
    Guid UserId,
    string ProviderName,
    long ProviderHotelId,
    long ProviderRoomId,
    int CityId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    int Rooms,
    string BoardType,
    long ExpectedTotalPriceMinor,
    string SearchCriteriaSnapshot,
    string SelectedHotelSnapshot,
    string SelectedRoomSnapshot,
    string? Fees,
    string GuestInfoSnapshot,
    string IdempotencyKey
) : IRequest<CreateHotelRequestResponse>;

public sealed record CreateHotelRequestResponse(
    Guid RequestId,
    string Status,
    DateTime ExpireAt,
    long TotalPrice,
    string Currency
);
