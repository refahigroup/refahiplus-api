namespace Refahi.Modules.Hotels.Infrastructure.Config;

public class PricingOptions
{
    public bool ApplyMargin { get; set; } = false;
    public int MarginPercent { get; set; } = 0;
}