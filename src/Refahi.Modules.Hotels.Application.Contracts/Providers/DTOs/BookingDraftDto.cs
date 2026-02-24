namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;

public sealed class BookingDraftDto
    {
        public long HotelId { get; set; }
        public long RoomId { get; set; }
        public DateOnly CheckIn { get; set; }
        public DateOnly CheckOut { get; set; }
        public int RoomsCount { get; set; }
        public IEnumerable<GuestDto> Guests { get; set; } = Enumerable.Empty<GuestDto>();
        public string BoardType { get; set; } = default!;
    }

