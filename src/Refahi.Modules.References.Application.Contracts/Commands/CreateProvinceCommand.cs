using MediatR;

namespace Refahi.Modules.References.Application.Contracts.Commands;

public sealed record CreateProvinceCommand(
    string Name,
    string Slug,
    int SortOrder
) : IRequest<CreateProvinceResponse>;

public sealed record CreateProvinceResponse(int Id, string Name);
