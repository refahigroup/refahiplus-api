using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Domain.Repositories;

namespace Refahi.Modules.References.Application.Features.Cities.GetCities;

public class GetCitiesQueryHandler : IRequestHandler<GetCitiesQuery, GetCitiesResponse>
{
    private readonly ICityRepository _cityRepository;

    public GetCitiesQueryHandler(ICityRepository cityRepository)
        => _cityRepository = cityRepository;

    public async Task<GetCitiesResponse> Handle(
        GetCitiesQuery request, CancellationToken cancellationToken)
    {
        var cities = await _cityRepository.GetAllAsync(request.ProvinceId, request.ActiveOnly, cancellationToken);

        var dtos = cities.Select(c => new CityDto(
            c.Id,
            c.Name,
            c.NameEn,
            c.Slug,
            c.ProvinceId,
            c.Province.Name,
            c.SortOrder,
            c.IsActive
        )).ToList();

        return new GetCitiesResponse(dtos);
    }
}
