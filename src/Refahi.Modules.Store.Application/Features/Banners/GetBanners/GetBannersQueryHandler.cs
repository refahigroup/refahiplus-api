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

        var filtered = request.BannerType.HasValue
            ? banners.Where(b => (short)b.BannerType == request.BannerType.Value).ToList()
            : banners;

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
