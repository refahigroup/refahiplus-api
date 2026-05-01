using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Banners;
using Refahi.Modules.Store.Application.Contracts.Queries.Banners;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Banners.GetAdminBanners;

public class GetAdminBannersQueryHandler : IRequestHandler<GetAdminBannersQuery, List<AdminBannerDto>>
{
    private readonly IBannerRepository _bannerRepo;

    public GetAdminBannersQueryHandler(IBannerRepository bannerRepo)
        => _bannerRepo = bannerRepo;

    public async Task<List<AdminBannerDto>> Handle(GetAdminBannersQuery request, CancellationToken cancellationToken)
    {
        var banners = await _bannerRepo.GetAllAsync(request.ModuleId, cancellationToken);

        var filtered = banners.AsEnumerable();

        if (request.BannerType.HasValue)
            filtered = filtered.Where(b => (short)b.BannerType == request.BannerType.Value);

        return filtered
            .OrderBy(b => b.SortOrder)
            .Select(b => new AdminBannerDto(
                b.Id, b.ModuleId, b.Title, b.ImageUrl, b.LinkUrl,
                b.BannerType.ToString(),
                b.SortOrder, b.IsActive,
                b.StartDate, b.EndDate))
            .ToList();
    }
}
