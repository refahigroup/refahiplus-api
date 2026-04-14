using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Banners;

public sealed record DeleteBannerCommand(int Id) : IRequest<DeleteBannerResponse>;

public sealed record DeleteBannerResponse(int Id);
