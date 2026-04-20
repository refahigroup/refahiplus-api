using MediatR;

namespace Refahi.Modules.References.Application.Contracts.Commands;

public sealed record UpdateCityCommand(
    int Id,
    string Name,
    string Slug,
    int SortOrder
) : IRequest<UpdateCityResponse>;

public sealed record UpdateCityResponse(int Id, string Name);
