using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Banners;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Banners.DeactivateBanner;

public class DeactivateBannerCommandHandler : IRequestHandler<DeactivateBannerCommand, DeactivateBannerResponse>
{
    private readonly IBannerRepository _bannerRepo;

    public DeactivateBannerCommandHandler(IBannerRepository bannerRepo)
        => _bannerRepo = bannerRepo;

    public async Task<DeactivateBannerResponse> Handle(DeactivateBannerCommand request, CancellationToken cancellationToken)
    {
        var banner = await _bannerRepo.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("بنر یافت نشد", "BANNER_NOT_FOUND");

        banner.Deactivate();
        await _bannerRepo.UpdateAsync(banner, cancellationToken);

        return new DeactivateBannerResponse(banner.Id);
    }
}
