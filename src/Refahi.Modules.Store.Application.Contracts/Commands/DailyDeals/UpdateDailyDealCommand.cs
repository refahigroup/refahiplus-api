using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;

public sealed record UpdateDailyDealCommand(
    int DealId,
    int DiscountPercent,
    string StartTime,
    string EndTime,
    bool IsActive
) : IRequest<UpdateDailyDealResponse>;

public sealed record UpdateDailyDealResponse(int Id, int DiscountPercent, bool IsActive);
