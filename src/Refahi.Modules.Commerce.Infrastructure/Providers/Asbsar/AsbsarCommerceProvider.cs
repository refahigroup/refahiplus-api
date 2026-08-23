using Refahi.Modules.Commerce.Application.Contracts.Abstraction;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Abstraction;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar;

public class AsbsarCommerceProvider : ICommerceProvider
{
    private readonly IAabsarApiClient _aabsarApiClient;

    public AsbsarCommerceProvider(IAabsarApiClient aabsarApiClient)
    {
        _aabsarApiClient = aabsarApiClient;
    }

    public string Key => "aabsar";
    public string Name => "آبسار";

    public async Task<IEnumerable<Offer>> GetOffersAsync(CancellationToken cancellationToken)
    {
        var showTimes = await _aabsarApiClient.GetShowtimesAsync(cancellationToken);

        if (showTimes?.Data == null || !showTimes.Data.Any())
            return Enumerable.Empty<Offer>();

        List<Offer> result = new List<Offer>();

        foreach(var showTime in showTimes!.Data!.Where(x => x.Capacity > 0))
        {
            if (showTime == null)
                continue;

            result.Add(new Offer
            {
                ProviderKey = Key,
                Id = $"{showTime.Id}-{showTime.EventId}",
                Tags = new[] { showTime?.Gender ?? "male", "adult" },
                Title = string.Format("{0} {1} {2}", showTime.EventTitle, showTime.Gender == "female" ? "آقایان" : "یانوان", showTime.Title),
                Subtitle = $"{showTime.VendorName} {Name}",
                OriginalPrice = showTime.AdultPrice,
                Price = showTime.AdultPrice
            });

            result.Add(new Offer
            {
                ProviderKey = Key,
                Id = $"{showTime.Id}-{showTime.EventId}",
                Tags = new[] { showTime?.Gender ?? "male", "child" },
                Title = string.Format("{0} {1} {2}", showTime.EventTitle, showTime.Gender == "female" ? "آقایان" : "یانوان", showTime.Title),
                Subtitle = $"{showTime.VendorName} {Name}",
                OriginalPrice = showTime.ChildOldPrice,
                Price = showTime.ChildPrice
            });
        }

        return result;
    }

    public async Task<IEnumerable<Seller>> GetSellersAsync(CancellationToken cancellationToken)
    {
        return new List<Seller>()
        {
            new Seller
            {
                Name = $"پارک آبی و استخر {Name}",
                Slug = "aabsar"
            }
        };
    }

    public async Task<IEnumerable<Product>> GetProductAsync(CancellationToken cancellationToken)
    {
        var showTimes = await _aabsarApiClient.GetShowtimesAsync(cancellationToken);

        if (showTimes?.Data == null || !showTimes.Data.Any())
            return Enumerable.Empty<Product>();

        return showTimes.Data
                        .Select(x => x.EventTitle)
                        .Distinct()
                        .Select(x => new Product
                        {
                            Name = x
                        });
    }
}
