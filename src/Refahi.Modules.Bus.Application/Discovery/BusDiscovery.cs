using FluentValidation;
using MediatR;
using Refahi.Modules.Bus.Application.Contracts;
using Refahi.Modules.Bus.Domain.Discovery;
using Refahi.Modules.References.Application.Contracts.Queries;

namespace Refahi.Modules.Bus.Application.Discovery;

public sealed class BusSearchValidator : AbstractValidator<BusSearchQuery>
{
    public BusSearchValidator(TimeProvider clock)
    {
        RuleFor(x => x.OriginCityId).GreaterThan(0).WithMessage("شهر مبدأ معتبر نیست.");
        RuleFor(x => x.DestinationCityId).GreaterThan(0).NotEqual(x => x.OriginCityId)
            .WithMessage("شهر مقصد باید با مبدأ متفاوت باشد.");
        RuleFor(x => x.DepartureDate).Must(date => date >= DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(clock.GetUtcNow(), "Asia/Tehran").DateTime))
            .WithMessage("تاریخ رفت معتبر نیست.");
        RuleFor(x => x.Company).MaximumLength(100).WithMessage("نام شرکت بیش از حد طولانی است.");
        RuleFor(x => x.BusType).MaximumLength(100).WithMessage("نوع اتوبوس معتبر نیست.");
        RuleFor(x => x.Sort).Must(x => x is null or "" or "price" or "time").WithMessage("مرتب‌سازی معتبر نیست.");
        RuleFor(x => x.Page).InclusiveBetween(1, 1000).WithMessage("شمارهٔ صفحه معتبر نیست.");
    }
}

public sealed class BusLocationsValidator : AbstractValidator<BusLocationsQuery>
{
    public BusLocationsValidator() =>
        RuleFor(x => x.Query).MaximumLength(100).WithMessage("عبارت جستجو بیش از حد طولانی است.");
}

public sealed class BusDiscoveryHandler(IBusDiscoveryRepository repository, ISender sender,
    IBusProvider provider, TimeProvider clock) :
    IRequestHandler<BusLocationsQuery, IReadOnlyList<BusLocation>>,
    IRequestHandler<BusSearchQuery, BusSearchResult>,
    IRequestHandler<BusOfferQuery, BusOfferDetails>
{
    public async Task<IReadOnlyList<BusLocation>> Handle(BusLocationsQuery request, CancellationToken ct)
    {
        var cities = await ReadCitiesAsync(null, ct);
        var query = Normalize(request.Query ?? "");
        return cities.Select(x => x.City)
            .Where(x => Normalize(x.Name).Contains(query, StringComparison.Ordinal))
            .OrderBy(x => x.Name, StringComparer.Create(new("fa-IR"), false)).ToArray();
    }

    public async Task<BusSearchResult> Handle(BusSearchQuery request, CancellationToken ct)
    {
        const int pageSize = 20;
        var cities = await ReadCitiesAsync([request.OriginCityId, request.DestinationCityId], ct);
        var origin = cities.SingleOrDefault(x => x.City.CityId == request.OriginCityId);
        var destination = cities.SingleOrDefault(x => x.City.CityId == request.DestinationCityId);
        if (origin.City is null || destination.City is null)
            throw new BusRequestException("این مسیر برای جستجوی اتوبوس فعال نیست.", 400);

        var services = await provider.SearchAsync(origin.RegionId,
            destination.RegionId, request.DepartureDate, ct);
        var filtered = services.Where(x =>
            (string.IsNullOrWhiteSpace(request.Company) || x.Company == request.Company)
            && (string.IsNullOrWhiteSpace(request.BusType) || x.BusType == request.BusType)
            && (!request.FromTime.HasValue || x.DepartureTime >= request.FromTime.Value));
        var sorted = (request.Sort == "time" ? filtered.OrderBy(x => x.DepartureTime) : filtered.OrderBy(x => x.PriceMinor))
            .ThenBy(x => x.Code, StringComparer.Ordinal).ToArray();
        var now = clock.GetUtcNow();
        var offers = sorted.Skip((request.Page - 1) * pageSize).Take(pageSize).Select(x =>
            BusOffer.Create(origin.City.CityId, destination.City.CityId, request.DepartureDate,
                new(x.Code, x.Company, x.BusType, x.DepartureTerminal, x.ArrivalTerminal,
                    x.DepartureTime, x.AvailableSeats, x.PriceMinor), now.AddMinutes(5))).ToArray();
        if (offers.Length != 0)
            await repository.StoreOffersAsync(offers, now, ct);
        return new(origin.City, destination.City, offers.Select(Map).ToArray(), request.Page,
            (sorted.Length + pageSize - 1) / pageSize);
    }

    public async Task<BusOfferDetails> Handle(BusOfferQuery request, CancellationToken ct)
    {
        var offer = await repository.GetOfferAsync(request.Token, ct);
        if (offer is null || offer.ExpiresAt <= clock.GetUtcNow())
            throw new BusRequestException("مهلت این پیشنهاد تمام شده است. دوباره جستجو کنید.", 404);
        var detail = await provider.DetailAsync(offer.Service.Code, ct);
        return new(Map(offer) with
        {
            Company = detail.Service.Company,
            BusType = detail.Service.BusType,
            DepartureTerminal = detail.Service.DepartureTerminal,
            ArrivalTerminal = detail.Service.ArrivalTerminal,
            DepartureTime = detail.Service.DepartureTime,
            PriceMinor = detail.Service.PriceMinor,
            AvailableSeats = detail.Seats.Count(seat => seat.Available)
        }, detail.Seats);
    }

    private async Task<(BusLocation City, int RegionId)[]> ReadCitiesAsync(int[]? cityIds, CancellationToken ct)
    {
        cityIds ??= (await sender.Send(new GetCitiesQuery(ActiveOnly: true), ct)).Cities.Select(x => x.Id).ToArray();
        if (cityIds.Length == 0) return [];
        var cities = await sender.Send(new GetCityGeographiesQuery(cityIds), ct);
        var regions = await provider.RegionsAsync(ct);
        var byId = regions.Where(region => region.Id > 0).GroupBy(region => region.Id)
            .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single());
        var byName = byId.Values.Where(region => region.Type == "City" && !string.IsNullOrWhiteSpace(region.Name))
            .ToLookup(region => Normalize(region.Name));
        var overrides = (await repository.CitiesAsync(ct)).ToDictionary(x => x.CityId, x => x.ProviderRegionId);
        var mapped = new List<(BusLocation City, int RegionId)>();
        foreach (var city in cities.Where(x => x.IsActive && x.CountryCode.Equals("IR", StringComparison.OrdinalIgnoreCase)))
        {
            if (overrides.TryGetValue(city.CityId, out var configured))
            {
                if (byId.TryGetValue(configured, out var region) && region.Type == "City")
                    mapped.Add((new(city.CityId, city.CityName, city.ProvinceName), configured));
                continue;
            }

            var matches = byName[Normalize(city.CityName)].Where(region => MatchesProvince(region, city.ProvinceName, byId)).ToArray();
            if (matches.Length == 1)
                mapped.Add((new(city.CityId, city.CityName, city.ProvinceName), matches[0].Id));
        }

        var reservedRegions = overrides.Values.ToHashSet();
        return mapped.GroupBy(x => x.RegionId)
            .SelectMany(group => reservedRegions.Contains(group.Key)
                ? group.Where(x => overrides.ContainsKey(x.City.CityId))
                : group.Take(group.Count() == 1 ? 1 : 0)).ToArray();
    }

    private static bool MatchesProvince(BusProviderRegion region, string province, IReadOnlyDictionary<int, BusProviderRegion> byId)
    {
        var target = Normalize(province);
        var visited = new HashSet<int>();
        var matches = false;
        while (visited.Add(region.Id) && region.ParentId.HasValue)
        {
            matches |= Normalize(region.ParentName) == target;
            if (!byId.TryGetValue(region.ParentId.Value, out var parent)) break;
            if (parent.Type == "Country") return matches && parent.Code.Equals("IR", StringComparison.OrdinalIgnoreCase);
            region = parent;
        }
        return matches;
    }

    private static string Normalize(string value) => value.Replace('ي', 'ی').Replace('ك', 'ک')
        .Replace("(استان)", "", StringComparison.Ordinal).Replace("استان ", "", StringComparison.Ordinal)
        .Replace("\u200c", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).Trim();

    private static BusOfferSummary Map(BusOffer offer) => new(offer.Id, offer.OriginCityId, offer.DestinationCityId,
        offer.DepartureDate, offer.Service.Company, offer.Service.BusType, offer.Service.DepartureTerminal,
        offer.Service.ArrivalTerminal, offer.Service.DepartureTime, offer.Service.AvailableSeats, offer.Service.PriceMinor);
}
