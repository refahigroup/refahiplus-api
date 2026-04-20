using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;

public sealed record DeactivateDailyDealCommand(int DealId) : IRequest<DeactivateDailyDealResponse>;

public sealed record DeactivateDailyDealResponse(int Id, bool IsActive);
