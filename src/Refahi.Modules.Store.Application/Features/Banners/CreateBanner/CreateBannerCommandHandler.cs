using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Banners;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Banners.CreateBanner;

public class CreateBannerCommandHandler : IRequestHandler<CreateBannerCommand, CreateBannerResponse>
{
    private readonly IBannerRepository _bannerRepo;

    public CreateBannerCommandHandler(IBannerRepository bannerRepo)
        => _bannerRepo = bannerRepo;

    public async Task<CreateBannerResponse> Handle(CreateBannerCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(BannerType), request.BannerType))
            throw new StoreDomainException("نوع بنر معتبر نیست", "INVALID_BANNER_TYPE");

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

        var banner = Banner.Create(
            request.ModuleId,
            request.Title,
            request.ImageUrl,
            (BannerType)request.BannerType,
            request.LinkUrl,
            request.SortOrder,
            startDate,
            endDate);

        await _bannerRepo.AddAsync(banner, cancellationToken);

        return new CreateBannerResponse(banner.Id, banner.Title);
    }
}
