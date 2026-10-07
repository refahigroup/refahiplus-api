using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Microsoft.Extensions.Options;

namespace Refahi.Modules.Cinema.Application.Features.Catalog;

public sealed record CinemaCitiesQuery : IRequest<IReadOnlyList<CinemaCity>>;
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

public sealed class CinemaCatalogHandlers(ICinemaProviderFactory factory, IOptions<CinemaOptions> options) :
    IRequestHandler<CinemaCitiesQuery, IReadOnlyList<CinemaCity>>, IRequestHandler<CinemaLandingQuery, CinemaLanding>, IRequestHandler<CinemaShowsQuery, CinemaCatalog>, IRequestHandler<CinemaShowQuery, CinemaShow>,
    IRequestHandler<CinemaPlacesQuery, IReadOnlyList<CinemaDisplayDay>>, IRequestHandler<CinemaSessionsQuery, IReadOnlyList<CinemaSession>>,
    IRequestHandler<CinemaSeatsQuery, CinemaSeatMap>, IRequestHandler<CinemaStatusesQuery, CinemaSeatStatuses>
{
    private ICinemaProvider Provider()
    {
        if (!options.Value.CatalogEnabled)
            throw new CinemaException("سرویس سینما هنوز فعال نشده است", 503);

        return factory.Get(options.Value.ProviderKey);
    }

    public Task<IReadOnlyList<CinemaCity>> Handle(CinemaCitiesQuery r, CancellationToken ct) => 
        Provider().CitiesAsync(ct);

    public Task<CinemaLanding> Handle(CinemaLandingQuery r, CancellationToken ct) => 
        Provider().LandingAsync(r.City, ct);
    public Task<CinemaCatalog> Handle(CinemaShowsQuery r, CancellationToken ct) => 
        Provider().ShowsAsync(r.Kind, r.City, r.Search, r.Page, ct);
    public Task<CinemaShow> Handle(CinemaShowQuery r, CancellationToken ct) => 
        Provider().ShowAsync(r.Id, ct);

    public Task<IReadOnlyList<CinemaDisplayDay>> Handle(CinemaPlacesQuery r, CancellationToken ct) => 
        Provider().PlacesAsync(r.Show, r.City, r.Search, ct);

    public Task<IReadOnlyList<CinemaSession>> Handle(CinemaSessionsQuery r, CancellationToken ct) => 
        Provider().SessionsAsync(r.Show, r.Place, r.Date, ct);

    public Task<CinemaSeatMap> Handle(CinemaSeatsQuery r, CancellationToken ct) => 
        Provider().SeatsAsync(r.Schedule, ct);

    public Task<CinemaSeatStatuses> Handle(CinemaStatusesQuery r, CancellationToken ct) => 
        Provider().StatusAsync(r.Schedule, ct);
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
            .Matches("^[0-9A-Za-z]{26}$")
            .WithMessage("شناسه سانس معتبر نیست");
    } 
}

public sealed class CinemaStatusesValidator : AbstractValidator<CinemaStatusesQuery>
{ 
    public CinemaStatusesValidator() 
    {
         RuleFor(x => x.Schedule)
            .Matches("^[0-9A-Za-z]{26}$")
            .WithMessage("شناسه سانس معتبر نیست"); 
    } 
}
