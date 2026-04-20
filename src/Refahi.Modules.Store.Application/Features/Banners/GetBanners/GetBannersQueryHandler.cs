using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Banners;
using Refahi.Modules.Store.Application.Contracts.Queries.Banners;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Banners.GetBanners;

public class GetBannersQueryHandler : IRequestHandler<GetBannersQuery, List<BannerDto>>
{
    private readonly IBannerRepository _bannerRepo;

    public GetBannersQueryHandler(IBannerRepository bannerRepo)
        => _bannerRepo = bannerRepo;

    public async Task<List<BannerDto>> Handle(GetBannersQuery request, CancellationToken cancellationToken)
    {
        var banners = await _bannerRepo.GetActiveAsync(cancellationToken);

        var filtered = banners.AsEnumerable();

        if (request.ModuleId.HasValue)
            filtered = filtered.Where(b => b.ModuleId == request.ModuleId.Value);

        if (request.BannerType.HasValue)
            filtered = filtered.Where(b => (short)b.BannerType == request.BannerType.Value);

        return filtered
            .OrderBy(b => b.SortOrder)
            .Select(b => new BannerDto(
                b.Id, b.Title, b.ImageUrl, b.LinkUrl,
                b.BannerType.ToString(),
                b.SortOrder, b.IsActive,
                b.StartDate, b.EndDate))
            .ToList();
    }
}
