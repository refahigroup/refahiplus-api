using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Infrastructure.Persistence;
using Refahi.Modules.Pools.Infrastructure.Providers.Aabsar.Abstraction;
using Refahi.Modules.Pools.Infrastructure.Providers.Aabsar.Options;
using Refahi.Modules.References.Application.Contracts.Queries;

namespace Refahi.Modules.Pools.Infrastructure.Providers.Aabsar;

internal sealed class AabsarCatalogueSync(PoolsDbContext db, IAabsarApiClient api, ISender sender,
    IPoolMutationLock mutationLock, TimeProvider clock)
{
    internal static readonly Guid LockId = new("33f424fd-ee9e-4bad-8fac-6e8a567e19ad");

    public async Task RunAsync(CancellationToken ct)
    {
        await using var lease = await mutationLock.AcquireAsync(LockId, ct);
        var response = await api.GetShowtimesAsync(ct);
        var data = response.Data;
        if (response.StatusCode is < 200 or >= 300 || data is null || data.Any(s => !AabsarApiClient.IsUlid(s.Id) ||
            string.IsNullOrWhiteSpace(s.EventId) || s.EventId.Length > 300 || s.Capacity < 0 ||
            s.AdultPrice < 0 || s.ChildPrice < 0 || s.AdultOldPrice < 0 || s.ChildOldPrice < 0 ||
            s.Time <= 0 || s.Time > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()) ||
            data.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != data.Count)
            throw new InvalidOperationException("فهرست سانس‌های آبسار معتبر نیست");

        var cities = await sender.Send(new GetCitiesQuery(ActiveOnly: true, Q: "اصفهان"), ct);
        var city = cities.Cities.Single(c =>
            CatalogueText.Normalize(c.Name) == "اصفهان" && CatalogueText.Normalize(c.ProvinceName) == "اصفهان");
        var venue = await db.AabsarVenues.SingleOrDefaultAsync(ct);
        if (venue is null)
        {
            venue = AabsarVenue.Create();
            db.AabsarVenues.Add(venue);
        }

        var products = await db.AabsarProducts.ToDictionaryAsync(x => x.EventId, ct);
        var showtimes = await db.AabsarShowtimes.ToDictionaryAsync(x => x.ShowtimeId, ct);
        foreach (var product in products.Values) product.Deactivate();
        foreach (var showtime in showtimes.Values) showtime.Deactivate();

        foreach (var group in data.GroupBy(x => x.EventId))
        {
            if (!products.TryGetValue(group.Key, out var product))
            {
                product = AabsarProduct.Create(group.Key);
                db.AabsarProducts.Add(product);
            }
            product.Publish(group.First().EventTitle ?? "بلیط استفاده از مجموعه آبی");
            foreach (var row in group)
            {
                if (!showtimes.TryGetValue(row.Id, out var showtime))
                {
                    showtime = AabsarShowtime.Create(row.Id, product.Id);
                    db.AabsarShowtimes.Add(showtime);
                }
                if (showtime.ProductId != product.Id)
                    throw new InvalidOperationException("شناسه سانس آبسار به محصول دیگری منتقل شده است");
                var gender = row.Gender switch { "female" => "بانوان", "male" => "آقایان", _ => null };
                var title = gender is null ? row.Title : $"{row.Title} - {gender}";
                showtime.Publish(title ?? product.Title, DateTimeOffset.FromUnixTimeMilliseconds(row.Time),
                    row.VendorName, row.Capacity, row.AdultPrice, row.ChildPrice, row.AdultOldPrice, row.ChildOldPrice);
            }
        }

        venue.Publish(city.Id, city.ProvinceId, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }
}

internal sealed class AabsarCatalogueWorker(IServiceScopeFactory scopes, IOptionsMonitor<AabsarOptions> options,
    TimeProvider clock, ILogger<AabsarCatalogueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var settings = options.CurrentValue;
            if (settings.CatalogueEnabled)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<AabsarCatalogueSync>().RunAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogWarning("Aabsar catalogue sync failed. ErrorType={ErrorType}", ex.GetType().Name); }
            }

            await Task.Delay(TimeSpan.FromSeconds(settings.CatalogCacheSeconds), clock, ct);
        }
    }
}
