namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;

public record GetAvailabilityByCityDto
{
    public GetAvailabilityByCityDto(AvailabilityByCitiesFilter? filter)
    {
        Filter = filter;
        Items = null;
    }

    public GetAvailabilityByCityDto(AvailabilityByCitiesFilter? filter, List<AvailabilityByCitiesItem>? items)
    {
        Filter = filter;
        Items = items;
    }

    public AvailabilityByCitiesFilter? Filter { get; set; }

    public List<AvailabilityByCitiesItem>? Items { get; set; }


    public static GetAvailabilityByCityDto Create(GetAvailabilityByCityQuery query)
    {
        var filter = new AvailabilityByCitiesFilter
        (
            query.MinPrice,
            query.MaxPrice,
            query.Adults,
            query.Children,
            query.AvailableRooms,
            (query.Stars) != null ? query.Stars.ToList() : null,
            (query.Accommodations) != null ? query.Accommodations.ToList() : null
        );

        return new GetAvailabilityByCityDto(filter);
    }
}

public record AvailabilityByCitiesFilter
(
    int? MinPrice,
    int? MaxPrice,
    int? Adults,
    int? Children,
    int? AvailableRooms,
    List<int>? Stars,
    List<string>? Accommodations
);

public record AvailabilityByCitiesItem
(
    int CityId,
    AvailabilityByCitiesHotel? Hotel,
    AvailabilityByCitiesRoom? Room
);

public record AvailabilityByCitiesHotel
(
     int Id,
     string Title,
     string? AccommodationType,
     string? AccommodationTitle,
     string? Address,
     int? Stars
);

public record AvailabilityByCitiesRoom
(
     int Id,
     string Title,
     int Price,
     int? PriceOff,
     int? DiscountPercent,
     int? ChildPrice,
     int? ExtraBedPrice,
     int? Children
);