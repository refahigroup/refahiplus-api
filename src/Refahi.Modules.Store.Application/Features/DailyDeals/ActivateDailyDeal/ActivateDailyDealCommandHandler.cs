using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.ActivateDailyDeal;

public class ActivateDailyDealCommandHandler : IRequestHandler<ActivateDailyDealCommand, ActivateDailyDealResponse>
{
    private readonly IDailyDealRepository _dealRepo;

    public ActivateDailyDealCommandHandler(IDailyDealRepository dealRepo)
        => _dealRepo = dealRepo;

    public async Task<ActivateDailyDealResponse> Handle(
        ActivateDailyDealCommand request, CancellationToken cancellationToken)
    {
        var deal = await _dealRepo.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("آفر روز یافت نشد", "DAILY_DEAL_NOT_FOUND");

        deal.Activate();

        await _dealRepo.UpdateAsync(deal, cancellationToken);

        return new ActivateDailyDealResponse(deal.Id, deal.IsActive);
    }
}
