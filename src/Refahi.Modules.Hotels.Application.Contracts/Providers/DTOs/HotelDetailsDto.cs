namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;

public sealed class HotelDetailsDto
{
    public long HotelId { get; set; }
    public string Name { get; set; } = default!;
    public string CityName { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Address { get; set; } = default!;
    public int Stars { get; set; }

    public IEnumerable<string> Images { get; set; } = Enumerable.Empty<string>();
    public IEnumerable<string> Facilities { get; set; } = Enumerable.Empty<string>();
    public IEnumerable<HotelRoomDto> Rooms { get; set; } = Enumerable.Empty<HotelRoomDto>();
}

