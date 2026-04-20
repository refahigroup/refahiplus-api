using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;

namespace Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;

public sealed record AdminGetDailyDealsQuery(int? ModuleId = null) : IRequest<List<AdminDailyDealDto>>;
