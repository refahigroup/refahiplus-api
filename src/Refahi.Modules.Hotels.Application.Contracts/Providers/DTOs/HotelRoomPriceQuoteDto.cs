namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;

public sealed record HotelRoomPriceQuoteRequest(
    int CityId,
    long HotelId,
    long RoomId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    int Rooms
);

public sealed record HotelRoomPriceQuoteDto(
    long HotelId,
    long RoomId,
    long OriginalPriceMinor,
    string Currency
);
