using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Microsoft.Extensions.Options;

namespace Refahi.Modules.Cinema.Application.Features.Catalog;

public sealed record CinemaCitiesQuery(bool Canonical = false) : IRequest<IReadOnlyList<CinemaCity>>;
public sealed record CinemaLandingQuery(int? City) : IRequest<CinemaLanding>;
public sealed record CinemaShowsQuery(string Kind, int? City, string? Search, int Page) : IRequest<CinemaCatalog>;
public sealed record CinemaShowQuery(string Id) : IRequest<CinemaShow>;
public sealed record CinemaPlacesQuery(string Show, int? City, string? Search) : IRequest<IReadOnlyList<CinemaDisplayDay>>;
public sealed record CinemaSessionsQuery(string Show, string Place, string? Date) : IRequest<IReadOnlyList<CinemaSession>>;
public sealed record CinemaSeatsQuery(string Schedule) : IRequest<CinemaSeatMap>;
public sealed record CinemaStatusesQuery(string Schedule) : IRequest<CinemaSeatStatuses>;

public sealed class CinemaLandingValidator : AbstractValidator<CinemaLandingQuery>
{ 
    public CinemaLandingValidator() 
    { 
        RuleFor(x => x.City)
            .GreaterThan(0).When(x => x.City.HasValue)
            .WithMessage("شهر معتبر نیست"); 
    } 
}

public sealed class CinemaShowsValidator : AbstractValidator<CinemaShowsQuery>
{
    public CinemaShowsValidator() 
    {
        RuleFor(x => x.Kind)
            .Must(x => x is "cinema" or "theater" or "art")
            .WithMessage("دسته نمایش معتبر نیست"); 
            
        RuleFor(x => x.Page)
            .InclusiveBetween(1, 10000)
            .WithMessage("شماره صفحه معتبر نیست"); 
            
        RuleFor(x => x.Search)
            .MaximumLength(100)
            .WithMessage("عبارت جستجو طولانی است"); 

        RuleFor(x => x.City)
            .GreaterThan(0)
            .When(x => x.City.HasValue)
            .WithMessage("شهر معتبر نیست"); 
    }
}

public sealed class CinemaSessionsValidator : AbstractValidator<CinemaSessionsQuery>
{
    public CinemaSessionsValidator()
    {
        RuleFor(x => x.Show)
            .NotEmpty()
            .MaximumLength(190)
            .WithMessage("شناسه نمایش معتبر نیست");

        RuleFor(x => x.Place)
            .NotEmpty()
            .MaximumLength(190)
            .WithMessage("شناسه محل معتبر نیست");

        RuleFor(x => x.Date)
            .Must(x => x == null || DateOnly.TryParseExact(x, "yyyy-MM-dd", out _))
            .WithMessage("تاریخ معتبر نیست");
    }
}

public sealed class CinemaCatalogHandlers(ICinemaProviderFactory factory, IOptions<CinemaOptions> options,
    CinemaMediaPublisher? media = null, ICinemaDiscoveryReader? discovery = null) :
    IRequestHandler<CinemaCitiesQuery, IReadOnlyList<CinemaCity>>, IRequestHandler<CinemaLandingQuery, CinemaLanding>, IRequestHandler<CinemaShowsQuery, CinemaCatalog>, IRequestHandler<CinemaShowQuery, CinemaShow>,
    IRequestHandler<CinemaPlacesQuery, IReadOnlyList<CinemaDisplayDay>>, IRequestHandler<CinemaSessionsQuery, IReadOnlyList<CinemaSession>>,
    IRequestHandler<CinemaSeatsQuery, CinemaSeatMap>, IRequestHandler<CinemaStatusesQuery, CinemaSeatStatuses>
{
    private readonly CinemaMediaPublisher publicMedia = media ?? new();

    private const string LegacyProvider = "iticket";

    private ICinemaProvider Provider(string? key = null)
    {
        EnsureEnabled();
        return factory.Get(key ?? options.Value.ProviderKey);
    }

    private void EnsureEnabled()
    {
        if (!options.Value.CatalogEnabled)
            throw new CinemaException("سرویس سینما هنوز فعال نشده است", 503);
    }

    private ICinemaDiscoveryReader Discovery()
    {
        EnsureEnabled();
        return discovery ?? throw new CinemaException("کاتالوگ سینما در دسترس نیست", 503);
    }

    public Task<IReadOnlyList<CinemaCity>> Handle(CinemaCitiesQuery r, CancellationToken ct) =>
        r.Canonical ? Discovery().CitiesAsync(ct) : Provider().CitiesAsync(ct);

    public async Task<CinemaLanding> Handle(CinemaLandingQuery r, CancellationToken ct) =>
        await publicMedia.PublishAsync(await Provider().LandingAsync(r.City, ct), options.Value.ProviderKey, ct);
    public async Task<CinemaCatalog> Handle(CinemaShowsQuery r, CancellationToken ct) =>
        await publicMedia.PublishAsync(await Provider().ShowsAsync(r.Kind, r.City, r.Search, r.Page, ct), options.Value.ProviderKey, ct);
    public async Task<CinemaShow> Handle(CinemaShowQuery r, CancellationToken ct) =>
        Guid.TryParseExact(r.Id, "D", out var show) ? await Discovery().ShowAsync(show, ct)
            : await publicMedia.PublishAsync(await Provider(LegacyProvider).ShowAsync(r.Id, ct), LegacyProvider, ct);

    public async Task<IReadOnlyList<CinemaDisplayDay>> Handle(CinemaPlacesQuery r, CancellationToken ct) =>
        Guid.TryParseExact(r.Show, "D", out var show) ? await Discovery().PlacesAsync(show, r.City, r.Search, ct)
            : await publicMedia.PublishAsync(await Provider(LegacyProvider).PlacesAsync(r.Show, r.City, r.Search, ct), LegacyProvider, ct);

    public Task<IReadOnlyList<CinemaSession>> Handle(CinemaSessionsQuery r, CancellationToken ct) =>
        Guid.TryParseExact(r.Show, "D", out var show)
            ? Discovery().SessionsAsync(show, Guid.TryParseExact(r.Place, "D", out var place) ? place
                : throw new CinemaException("شناسه محل معتبر نیست", 400), r.Date, ct)
            : Provider(LegacyProvider).SessionsAsync(r.Show, r.Place, r.Date, ct);

    public async Task<CinemaSeatMap> Handle(CinemaSeatsQuery r, CancellationToken ct)
    {
        EnsureEnabled();
        var schedule = await CinemaScheduleIdentity.ResolveAsync(r.Schedule, discovery, ct);
        var map = await factory.Get(schedule.ProviderKey).SeatsAsync(schedule.ExternalId, ct);
        return await publicMedia.PublishAsync(map with { ScheduleId = r.Schedule }, schedule.ProviderKey, ct);
    }

    public async Task<CinemaSeatStatuses> Handle(CinemaStatusesQuery r, CancellationToken ct)
    {
        EnsureEnabled();
        var schedule = await CinemaScheduleIdentity.ResolveAsync(r.Schedule, discovery, ct);
        return await factory.Get(schedule.ProviderKey).StatusAsync(schedule.ExternalId, ct);
    }
}

public sealed class CinemaShowValidator : AbstractValidator<CinemaShowQuery>
{ 
    public CinemaShowValidator() 
    { 
        RuleFor(x => x.Id)
            .NotEmpty()
            .MaximumLength(190)
            .WithMessage("شناسه نمایش معتبر نیست"); 
    } 
}

public sealed class CinemaPlacesValidator : AbstractValidator<CinemaPlacesQuery>
{ 
    public CinemaPlacesValidator() 
    { 
        RuleFor(x => x.Show)
            .NotEmpty()
            .MaximumLength(190)
            .WithMessage("شناسه نمایش معتبر نیست"); 

        RuleFor(x => x.Search)
            .MaximumLength(100)
            .WithMessage("عبارت جستجو طولانی است"); 

        RuleFor(x => x.City)
            .GreaterThan(0)
            .When(x => x.City.HasValue)
            .WithMessage("شهر معتبر نیست"); 
    } 
}

public sealed class CinemaSeatsValidator : AbstractValidator<CinemaSeatsQuery>
{ 
    public CinemaSeatsValidator() 
    { 
        RuleFor(x => x.Schedule)
            .Must(CinemaScheduleIdentity.IsValid)
            .WithMessage("شناسه سانس معتبر نیست");
    } 
}

public sealed class CinemaStatusesValidator : AbstractValidator<CinemaStatusesQuery>
{ 
    public CinemaStatusesValidator() 
    {
         RuleFor(x => x.Schedule)
            .Must(CinemaScheduleIdentity.IsValid)
            .WithMessage("شناسه سانس معتبر نیست"); 
    } 
}
