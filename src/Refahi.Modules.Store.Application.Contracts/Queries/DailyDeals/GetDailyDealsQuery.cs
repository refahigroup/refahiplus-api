using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;

public sealed record GetDailyDealsQuery(
    BannerOwnerType OwnerType,
    string OwnerId
) : IRequest<List<DailyDealDto>>;
