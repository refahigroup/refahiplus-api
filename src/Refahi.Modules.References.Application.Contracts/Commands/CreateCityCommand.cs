using MediatR;

namespace Refahi.Modules.References.Application.Contracts.Commands;

public sealed record CreateCityCommand(
    string Name,
    string Slug,
    int ProvinceId,
    int SortOrder
) : IRequest<CreateCityResponse>;

public sealed record CreateCityResponse(int Id, string Name);
