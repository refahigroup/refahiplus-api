namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

public sealed class SnappTripHotelReviewsResponse
{
    public int hotel_id { get; set; }
    public List<SnappTripHotelReview> reviews { get; set; } = new();
}

public sealed class SnappTripHotelReview
{
    public int id { get; set; }
    public int hotel_id { get; set; }
    public int user_id { get; set; }
    public string fullname { get; set; } = default!;
    public string comment { get; set; } = default!;
    public bool has_ever_booked { get; set; }
    public bool recommended { get; set; }
    public double rate_overall { get; set; }
    public double rate_clean { get; set; }
    public double rate_location { get; set; }
    public double rate_staff { get; set; }
    public double rate_services { get; set; }
    public double rate_sleep_quality { get; set; }
    public double rate_value_for_money { get; set; }
    public double rate_collective_avg { get; set; }
    public int rate_facility { get; set; }
    public int rate_food_quality { get; set; }
    public double comment_risk_level { get; set; }
    public string status { get; set; } = default!;
    public long registered_date { get; set; }
    public long update_date { get; set; }
}
