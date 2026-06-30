using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest;

public sealed record CreateHotelRequestCommand(
    Guid UserId,
    string ProviderName,
    long ProviderHotelId,
    long ProviderRoomId,
    string SearchCriteriaSnapshot,
    string SelectedHotelSnapshot,
    string SelectedRoomSnapshot,
    long TotalPrice,
    string Currency,
    string Breakdown,
    string? Fees,
    string GuestInfoSnapshot,
    string IdempotencyKey) : IRequest<CreateHotelRequestResponse>;

public sealed record CreateHotelRequestResponse(
    Guid RequestId,
    string Status,
    DateTime ExpireAt,
    long TotalPrice,
    string Currency);
