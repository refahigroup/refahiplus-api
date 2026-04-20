using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.DeactivateDailyDeal;

public class DeactivateDailyDealCommandHandler : IRequestHandler<DeactivateDailyDealCommand, DeactivateDailyDealResponse>
{
    private readonly IDailyDealRepository _dealRepo;

    public DeactivateDailyDealCommandHandler(IDailyDealRepository dealRepo)
        => _dealRepo = dealRepo;

    public async Task<DeactivateDailyDealResponse> Handle(DeactivateDailyDealCommand request, CancellationToken cancellationToken)
    {
        var deal = await _dealRepo.GetByIdAsync(request.DealId, cancellationToken)
            ?? throw new StoreDomainException("پیشنهاد ویژه یافت نشد", "DAILY_DEAL_NOT_FOUND");

        deal.Deactivate();

        await _dealRepo.UpdateAsync(deal, cancellationToken);

        return new DeactivateDailyDealResponse(deal.Id, deal.IsActive);
    }
}
