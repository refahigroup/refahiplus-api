namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;

public class HotelSearchByCityResultDto
{
    public long HotelId { get; set; }
    public string Name { get; set; } = default!;
    public int CityId { get; set; }
    public int Star { get; set; }
    public decimal MinPrice { get; set; }
    public string Currency { get; set; } = default!;
    public string? ThumbnailUrl { get; set; }
}
