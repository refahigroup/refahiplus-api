using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.UpdateDailyDeal;

public class UpdateDailyDealCommandHandler : IRequestHandler<UpdateDailyDealCommand, UpdateDailyDealResponse>
{
    private readonly IDailyDealRepository _dealRepo;

    public UpdateDailyDealCommandHandler(IDailyDealRepository dealRepo)
        => _dealRepo = dealRepo;

    public async Task<UpdateDailyDealResponse> Handle(UpdateDailyDealCommand request, CancellationToken cancellationToken)
    {
        var deal = await _dealRepo.GetByIdAsync(request.DealId, cancellationToken)
            ?? throw new StoreDomainException("پیشنهاد ویژه یافت نشد", "DAILY_DEAL_NOT_FOUND");

        if (!DateTimeOffset.TryParse(request.StartTime, out var startTime))
            throw new StoreDomainException("زمان شروع معتبر نیست", "INVALID_START_TIME");

        if (!DateTimeOffset.TryParse(request.EndTime, out var endTime))
            throw new StoreDomainException("زمان پایان معتبر نیست", "INVALID_END_TIME");

        deal.UpdateInfo(request.DiscountPercent, startTime, endTime, request.IsActive);

        await _dealRepo.UpdateAsync(deal, cancellationToken);

        return new UpdateDailyDealResponse(deal.Id, deal.DiscountPercent, deal.IsActive);
    }
}
