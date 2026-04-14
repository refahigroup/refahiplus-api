using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;

public sealed record CreateDailyDealCommand(
    Guid ProductId, int DiscountPercent,
    string StartTime, string EndTime
) : IRequest<CreateDailyDealResponse>;

public sealed record CreateDailyDealResponse(int Id, Guid ProductId, int DiscountPercent);
