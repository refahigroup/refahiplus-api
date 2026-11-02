using FluentValidation;
using MediatR;
using Refahi.Modules.References.Application.Contracts.Queries;

namespace Refahi.Modules.References.Application.Features.Cities.GetCityGeographies;

public sealed class GetCityGeographiesValidator : AbstractValidator<GetCityGeographiesQuery>
{
    public GetCityGeographiesValidator()
    {
        RuleFor(x => x.CityIds).NotNull().WithMessage("شناسه شهرها الزامی است.");
        RuleForEach(x => x.CityIds).GreaterThan(0).WithMessage("شناسه شهر معتبر نیست.");
    }
}

public sealed class GetCityGeographiesHandler(ICityGeographyReader reader)
    : IRequestHandler<GetCityGeographiesQuery, IReadOnlyList<CityGeography>>
{
    public Task<IReadOnlyList<CityGeography>> Handle(GetCityGeographiesQuery request, CancellationToken ct) =>
        reader.ReadAsync(request.CityIds, ct);
}
