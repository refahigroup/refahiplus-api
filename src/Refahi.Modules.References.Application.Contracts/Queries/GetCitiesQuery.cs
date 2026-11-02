using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;

namespace Refahi.Modules.References.Application.Contracts.Queries;

public sealed record GetCitiesQuery(int? ProvinceId = null, bool ActiveOnly = false, string? Q = null)
    : IRequest<GetCitiesResponse>;

public sealed record GetCitiesResponse(List<CityDto> Cities);
