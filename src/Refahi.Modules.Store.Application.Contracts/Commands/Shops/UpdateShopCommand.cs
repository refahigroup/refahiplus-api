using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Shops;

public sealed record UpdateShopCommand(
    Guid Id,
    string Name,
    string? Description,
    string? City,
    string? Address,
    string? ContactPhone,
    string? LogoUrl,
    string? CoverImageUrl
) : IRequest<UpdateShopResponse>;

public sealed record UpdateShopResponse(Guid Id, string Name);
