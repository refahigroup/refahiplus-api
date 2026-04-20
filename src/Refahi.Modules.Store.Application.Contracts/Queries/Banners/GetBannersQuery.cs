using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Banners;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Banners;

public sealed record GetBannersQuery(
    int? ModuleId = null,
    short? BannerType = null
) : IRequest<List<BannerDto>>;
