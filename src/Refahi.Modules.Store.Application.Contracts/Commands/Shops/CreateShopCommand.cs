using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Shops;

public sealed record CreateShopCommand(
    string Name,
    string Slug,
    short ShopType,
    Guid ProviderId,
    string? City,
    string? Address,
    string? Description,
    string? ContactPhone
) : IRequest<CreateShopResponse>;

public sealed record CreateShopResponse(Guid Id, string Name, string Slug, string Status);
