using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Banners;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Banners.DeleteBanner;

public class DeleteBannerCommandHandler : IRequestHandler<DeleteBannerCommand, DeleteBannerResponse>
{
    private readonly IBannerRepository _bannerRepo;

    public DeleteBannerCommandHandler(IBannerRepository bannerRepo)
        => _bannerRepo = bannerRepo;

    public async Task<DeleteBannerResponse> Handle(DeleteBannerCommand request, CancellationToken cancellationToken)
    {
        var banner = await _bannerRepo.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("بنر یافت نشد", "BANNER_NOT_FOUND");

        await _bannerRepo.DeleteAsync(banner, cancellationToken);

        return new DeleteBannerResponse(banner.Id);
    }
}
