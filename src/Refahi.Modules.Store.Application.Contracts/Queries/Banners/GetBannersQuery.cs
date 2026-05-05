using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Banners;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Banners;

public sealed record GetBannersQuery(
    BannerOwnerType OwnerType,
    string OwnerId,
    short? BannerType = null
) : IRequest<List<BannerDto>>;
