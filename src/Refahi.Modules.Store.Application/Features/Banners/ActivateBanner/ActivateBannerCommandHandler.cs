using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Banners;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Banners.ActivateBanner;

public class ActivateBannerCommandHandler : IRequestHandler<ActivateBannerCommand, ActivateBannerResponse>
{
    private readonly IBannerRepository _bannerRepo;

    public ActivateBannerCommandHandler(IBannerRepository bannerRepo)
        => _bannerRepo = bannerRepo;

    public async Task<ActivateBannerResponse> Handle(ActivateBannerCommand request, CancellationToken cancellationToken)
    {
        var banner = await _bannerRepo.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("بنر یافت نشد", "BANNER_NOT_FOUND");

        banner.Activate();
        await _bannerRepo.UpdateAsync(banner, cancellationToken);

        return new ActivateBannerResponse(banner.Id);
    }
}
