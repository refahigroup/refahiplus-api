using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Banners;

public sealed record UpdateBannerCommand(
    int Id, string Title, string ImageUrl, string? LinkUrl,
    int SortOrder, bool IsActive, string? StartDate, string? EndDate
) : IRequest<UpdateBannerResponse>;

public sealed record UpdateBannerResponse(int Id);
