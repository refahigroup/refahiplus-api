using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Banners;
using Refahi.Modules.Store.Application.Contracts.Queries.Banners;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Banners.GetBanners;

public class GetBannersQueryHandler : IRequestHandler<GetBannersQuery, List<BannerDto>>
{
    private readonly IBannerRepository _bannerRepo;
    private readonly IPathService _pathService;

    public GetBannersQueryHandler(IBannerRepository bannerRepo, IPathService pathService)
    {
        _bannerRepo = bannerRepo;
        _pathService = pathService;
    }

    public async Task<List<BannerDto>> Handle(GetBannersQuery request, CancellationToken cancellationToken)
    {
        List<Banner> banners;

        if (request.OwnerType == BannerOwnerType.Module)
        {
            if (!int.TryParse(request.OwnerId, out var moduleId))
                return new();
            banners = await _bannerRepo.GetActiveByModuleAsync(moduleId, cancellationToken);
        }
        else if (request.OwnerType == BannerOwnerType.Shop)
        {
            if (!Guid.TryParse(request.OwnerId, out var shopId))
                return new();
            banners = await _bannerRepo.GetActiveByShopAsync(shopId, cancellationToken);
        }
        else
        {
            return new();
        }

        var filtered = banners.AsEnumerable();

        if (request.BannerType.HasValue)
            filtered = filtered.Where(b => (short)b.BannerType == request.BannerType.Value);

        return filtered
            .OrderBy(b => b.SortOrder)
            .Select(b => new BannerDto(
                b.Id, b.Title, _pathService.MakeAbsoluteMediaUrl(b.ImageUrl), b.LinkUrl,
                b.BannerType.ToString(),
                b.SortOrder, b.IsActive,
                b.StartDate, b.EndDate))
            .ToList();
    }
}
