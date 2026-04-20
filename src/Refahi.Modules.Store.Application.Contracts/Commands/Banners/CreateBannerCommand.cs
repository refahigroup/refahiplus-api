using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Banners;

public sealed record CreateBannerCommand(
    int ModuleId,
    string Title, string ImageUrl, short BannerType, string? LinkUrl,
    int SortOrder, string? StartDate, string? EndDate
) : IRequest<CreateBannerResponse>;

public sealed record CreateBannerResponse(int Id, string Title);
