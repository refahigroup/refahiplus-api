using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;

public sealed record ActivateDailyDealCommand(int Id) : IRequest<ActivateDailyDealResponse>;

public sealed record ActivateDailyDealResponse(int Id, bool IsActive);
