using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;

namespace Refahi.Modules.References.Application.Contracts.Queries;

public sealed record GetCityByIdQuery(int Id)  : IRequest<CityDto?>;
