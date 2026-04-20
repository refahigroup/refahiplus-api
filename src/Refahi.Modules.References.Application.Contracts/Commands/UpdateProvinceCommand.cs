using MediatR;

namespace Refahi.Modules.References.Application.Contracts.Commands;

public sealed record UpdateProvinceCommand(
    int Id,
    string Name,
    string Slug,
    int SortOrder
) : IRequest<UpdateProvinceResponse>;

public sealed record UpdateProvinceResponse(int Id, string Name);
