using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Sessions;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Sessions.CreateSession;

public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, CreateSessionResponse>
{
    private readonly IProductRepository _productRepo;

    public CreateSessionCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<CreateSessionResponse> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        if (product.SalesModel != SalesModel.SessionBased)
            throw new StoreDomainException("این محصول سانسی نیست", "NOT_SESSION_PRODUCT");

        if (!DateOnly.TryParse(request.Date, out var date))
            throw new StoreDomainException("تاریخ وارد شده معتبر نیست", "INVALID_DATE");

        if (!TimeOnly.TryParse(request.StartTime, out var startTime))
            throw new StoreDomainException("زمان شروع وارد شده معتبر نیست", "INVALID_START_TIME");

        if (!TimeOnly.TryParse(request.EndTime, out var endTime))
            throw new StoreDomainException("زمان پایان وارد شده معتبر نیست", "INVALID_END_TIME");

        product.AddSession(date, startTime, endTime, request.Capacity, request.Title, request.PriceAdjustment);

        await _productRepo.UpdateAsync(product, cancellationToken);

        var session = product.Sessions.Last();
        return new CreateSessionResponse(
            session.Id,
            session.Date.ToString("yyyy-MM-dd"),
            session.StartTime.ToString("HH:mm"),
            session.EndTime.ToString("HH:mm"),
            session.Capacity);
    }
}
