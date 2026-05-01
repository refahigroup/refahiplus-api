using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Banners;

public sealed record DeactivateBannerCommand(int Id) : IRequest<DeactivateBannerResponse>;
public sealed record DeactivateBannerResponse(int Id);
