namespace Refahi.Modules.Hotels.Infrastructure.Config;

public class HotelsOptions
{
    public string ConnectionString { get; set; } = default!;
    public BookingOptions Booking { get; set; } = new();
    public PricingOptions Pricing { get; set; } = new();
}