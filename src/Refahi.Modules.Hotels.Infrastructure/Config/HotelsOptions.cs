namespace Refahi.Modules.Hotels.Infrastructure.Config;

public class HotelsOptions
{
    public BookingOptions Booking { get; set; } = new();
    public PricingOptions Pricing { get; set; } = new();
}