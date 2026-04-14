using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.CreateDailyDeal;

public class CreateDailyDealCommandHandler : IRequestHandler<CreateDailyDealCommand, CreateDailyDealResponse>
{
    private readonly IDailyDealRepository _dealRepo;
    private readonly IProductRepository _productRepo;

    public CreateDailyDealCommandHandler(IDailyDealRepository dealRepo, IProductRepository productRepo)
    {
        _dealRepo = dealRepo;
        _productRepo = productRepo;
    }

    public async Task<CreateDailyDealResponse> Handle(CreateDailyDealCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        if (!DateTimeOffset.TryParse(request.StartTime, out var startTime))
            throw new StoreDomainException("زمان شروع معتبر نیست", "INVALID_START_TIME");

        if (!DateTimeOffset.TryParse(request.EndTime, out var endTime))
            throw new StoreDomainException("زمان پایان معتبر نیست", "INVALID_END_TIME");

        var deal = DailyDeal.Create(request.ProductId, request.DiscountPercent, startTime, endTime);

        await _dealRepo.AddAsync(deal, cancellationToken);

        return new CreateDailyDealResponse(deal.Id, deal.ProductId, deal.DiscountPercent);
    }
}
