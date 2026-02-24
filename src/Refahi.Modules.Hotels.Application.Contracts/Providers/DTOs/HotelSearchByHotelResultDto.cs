namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;

public class HotelSearchByHotelResultDto
{
    public long HotelId { get; set; }
    public string Name { get; set; } = default!;
    public string CityName { get; set; } = default!;
    public int Stars { get; set; }
    public string AccommodationType { get; set; } = default!;
    public decimal MinCustomerPrice { get; set; }
    public string? ThumbnailUrl { get; set; }
}
