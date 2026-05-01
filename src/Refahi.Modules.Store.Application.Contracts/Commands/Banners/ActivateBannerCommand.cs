using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Banners;

public sealed record ActivateBannerCommand(int Id) : IRequest<ActivateBannerResponse>;
public sealed record ActivateBannerResponse(int Id);
