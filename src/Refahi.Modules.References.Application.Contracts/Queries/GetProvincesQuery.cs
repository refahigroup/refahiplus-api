using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;

namespace Refahi.Modules.References.Application.Contracts.Queries;

public sealed record GetProvincesQuery(
    bool ActiveOnly = false
) : IRequest<GetProvincesResponse>;

public sealed record GetProvincesResponse(List<ProvinceDto> Provinces);
