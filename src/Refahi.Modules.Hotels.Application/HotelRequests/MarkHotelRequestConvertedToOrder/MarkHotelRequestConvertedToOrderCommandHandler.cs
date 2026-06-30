using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.MarkHotelRequestConvertedToOrder;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;

namespace Refahi.Modules.Hotels.Application.HotelRequests.MarkHotelRequestConvertedToOrder;

public sealed class MarkHotelRequestConvertedToOrderCommandHandler
    : IRequestHandler<MarkHotelRequestConvertedToOrderCommand>
{
    private readonly IHotelRequestRepository _repository;
    private readonly IHotelBookingSagaRepository _sagaRepository;

    public MarkHotelRequestConvertedToOrderCommandHandler(
        IHotelRequestRepository repository,
        IHotelBookingSagaRepository sagaRepository)
    {
        _repository = repository;
        _sagaRepository = sagaRepository;
    }

    public async Task<Unit> Handle(
        MarkHotelRequestConvertedToOrderCommand request,
        CancellationToken cancellationToken)
    {
        var hotelRequest = await _repository.GetAsync(request.RequestId, cancellationToken)
            ?? throw new InvalidOperationException("درخواست هتل یافت نشد");

        if (hotelRequest.UserId != request.UserId)
            throw new UnauthorizedAccessException("دسترسی به این درخواست هتل مجاز نیست");

        var now = DateTime.UtcNow;
        var saga = await _sagaRepository.GetByHotelRequestIdAsync(hotelRequest.Id, cancellationToken);
        if (saga is null)
        {
            saga = HotelBookingSagaState.Start(hotelRequest.UserId, hotelRequest.Id, now);
            await _sagaRepository.AddAsync(saga, cancellationToken);
        }

        hotelRequest.ConvertToOrder(request.OrderId, now);
        saga.MarkOrderCreated(request.OrderId, now);
        saga.MarkPaymentPending(now);
        await _repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
