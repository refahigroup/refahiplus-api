using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Sessions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Sessions.GetProductSessions;

public class GetProductSessionsQueryHandler : IRequestHandler<GetProductSessionsQuery, List<ProductSessionDto>>
{
    private readonly IProductSessionRepository _sessionRepo;

    public GetProductSessionsQueryHandler(IProductSessionRepository sessionRepo)
        => _sessionRepo = sessionRepo;

    public async Task<List<ProductSessionDto>> Handle(GetProductSessionsQuery request, CancellationToken cancellationToken)
    {
        var sessions = await _sessionRepo.GetAvailableByProductIdAsync(request.ProductId, cancellationToken);

        return sessions.Select(s => new ProductSessionDto(
            s.Id,
            s.Date.ToString("yyyy-MM-dd"),
            s.StartTime.ToString("HH:mm"),
            s.EndTime.ToString("HH:mm"),
            s.Title,
            s.Capacity,
            s.SoldCount,
            s.RemainingCapacity,
            s.PriceAdjustment,
            s.IsAvailable))
            .ToList();
    }
}
