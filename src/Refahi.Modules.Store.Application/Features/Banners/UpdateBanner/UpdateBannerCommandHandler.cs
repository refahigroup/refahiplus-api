using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Banners;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Banners.UpdateBanner;

public class UpdateBannerCommandHandler : IRequestHandler<UpdateBannerCommand, UpdateBannerResponse>
{
    private readonly IBannerRepository _bannerRepo;

    public UpdateBannerCommandHandler(IBannerRepository bannerRepo)
        => _bannerRepo = bannerRepo;

    public async Task<UpdateBannerResponse> Handle(UpdateBannerCommand request, CancellationToken cancellationToken)
    {
        var banner = await _bannerRepo.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("بنر یافت نشد", "BANNER_NOT_FOUND");

        DateTimeOffset? startDate = null;
        DateTimeOffset? endDate = null;

        if (!string.IsNullOrWhiteSpace(request.StartDate))
        {
            if (!DateTimeOffset.TryParse(request.StartDate, out var parsedStart))
                throw new StoreDomainException("تاریخ شروع معتبر نیست", "INVALID_START_DATE");
            startDate = parsedStart.ToUniversalTime();
        }

        if (!string.IsNullOrWhiteSpace(request.EndDate))
        {
            if (!DateTimeOffset.TryParse(request.EndDate, out var parsedEnd))
                throw new StoreDomainException("تاریخ پایان معتبر نیست", "INVALID_END_DATE");
            endDate = parsedEnd.ToUniversalTime();
        }

        banner.Update(request.Title, request.ImageUrl, request.LinkUrl,
            request.SortOrder, request.IsActive, startDate, endDate);

        await _bannerRepo.UpdateAsync(banner, cancellationToken);

        return new UpdateBannerResponse(banner.Id);
    }
}
