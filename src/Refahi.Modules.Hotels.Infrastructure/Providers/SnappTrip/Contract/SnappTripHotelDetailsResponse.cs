namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

public sealed class SnappTripHotelDetailsResponse
{
    public SnappTripHotelDetail hotel { get; set; } = new();
}

public sealed class SnappTripHotelDetail
{
    public int id { get; set; }
    public string title { get; set; } = default!;
    public string title_en { get; set; } = default!;
    public string accommodation_title { get; set; } = default!;
    public string accommodation_type { get; set; } = default!;
    public SnappTripHotelDetailReviews reviews { get; set; } = new ();
    public string description { get; set; } = default!;
    public SnappTripHotelDetailGalleryItem cover { get; set; } = new();
    public List<SnappTripHotelDetailGalleryItem> gallery { get; set; } = new();
    public int stars { get; set; }
    public string address { get; set; } = default!;
    public SnappTripCity city { get; set; } = new();
    public SnappTripHotelDetailLocation location { get; set; } = new () ;
    public List<SnappTripHotelDetailFacility> facilities { get; set; } = new();
    public SnappTripHotelDetailPolicy policies { get; set; } = new ();
    public bool is_marketplace { get; set; } = false;
    public bool enabled { get; set; } = true;

}

public sealed class SnappTripHotelDetailReviews
{
    public float ratings { get; set; } = 0.0f;
    public int reviews { get; set; } = 0;
}

public sealed class SnappTripCity
{
    public int id { get; set; }
    public string title { get; set; } = default!;
}

public sealed class SnappTripHotelDetailFacility
{
    public string title { get; set; }
    public string icon { get; set; }
}

public sealed class SnappTripHotelDetailLocation
{
    public decimal lat { get; set; } = 0;
    public decimal lon { get; set; } = 0;
}

public sealed class SnappTripHotelDetailGalleryItem
{
    public string url { get; set; } = default!;
    public string title { get; set; } = default!;
    public string description { get; set; } = default!;
}

public sealed class SnappTripHotelDetailPolicy
{
    public int child_age { get; set; } = 0;
    public int infant_age { get; set; } = 0;
    public string check_in_time { get; set; } = default!;
    public string check_out_time { get; set; } = default!;
    public bool foreigners_fee { get; set; } = false;
    public string cancellation { get; set; } = default!;
}

//policies: child_age: int, infant_age: int, check_in_time: string, check_out_time: string, foreigners_fee: false, cancellation: string
