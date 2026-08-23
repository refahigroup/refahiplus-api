using System;
using System.Collections.Generic;
using System.Text;

namespace Refahi.Modules.Commerce.Application.Contracts.Abstraction;

public class Offer
{
    public string ProviderKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public string? ImageUrl { get; set; }

    public string? IconUrl { get; set; }

    public string? PriceText { get; set; }

    public string? BadgeText { get; set; }

    public string? BadgeVariant { get; set; }

    public decimal? Rating { get; set; }

    public string? Url { get; set; }

    public bool IsDisabled { get; set; }
}
