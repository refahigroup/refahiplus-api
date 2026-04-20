using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Domain.Repositories;

namespace Refahi.Modules.References.Application.Features.Cities.GetCityById;

public class GetCityByIdQueryHandler : IRequestHandler<GetCityByIdQuery, CityDto?>
{
    private readonly ICityRepository _cityRepository;

    public GetCityByIdQueryHandler(ICityRepository cityRepository)
        => _cityRepository = cityRepository;

    public async Task<CityDto?> Handle(
        GetCityByIdQuery request, CancellationToken cancellationToken)
    {
        var city = await _cityRepository.GetByIdAsync(request.Id, cancellationToken);

        if (city is null)
            return null;

        return new CityDto(
            city.Id,
            city.Name,
            city.Slug,
            city.ProvinceId,
            city.Province.Name,
            city.SortOrder,
            city.IsActive
        );
    }
}
