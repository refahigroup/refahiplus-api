using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;

namespace Refahi.Modules.Hotels.Application.HotelRequests.CreateHotelRequest;

public sealed class CreateHotelRequestCommandHandler
    : IRequestHandler<CreateHotelRequestCommand, CreateHotelRequestResponse>
{
    private static readonly TimeSpan RequestTtl = TimeSpan.FromMinutes(20);
    private readonly IHotelRequestRepository _repository;
    private readonly IHotelBookingSagaRepository _sagaRepository;
    private readonly ILogger<CreateHotelRequestCommandHandler> _logger;

    public CreateHotelRequestCommandHandler(
        IHotelRequestRepository repository,
        IHotelBookingSagaRepository sagaRepository,
        ILogger<CreateHotelRequestCommandHandler> logger)
    {
        _repository = repository;
        _sagaRepository = sagaRepository;
        _logger = logger;
    }

    public async Task<CreateHotelRequestResponse> Handle(
        CreateHotelRequestCommand request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        var existing = await _repository.GetByIdempotencyKeyAsync(
            request.UserId,
            idempotencyKey,
            cancellationToken);

        if (existing is not null)
        {
            var existingSaga = await _sagaRepository.GetByHotelRequestIdAsync(existing.Id, cancellationToken);
            if (existingSaga is null)
            {
                existingSaga = HotelBookingSagaState.Start(existing.UserId, existing.Id, DateTime.UtcNow);
                await _sagaRepository.AddAsync(existingSaga, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);
            }

            using var existingScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["UserId"] = existing.UserId,
                ["SagaId"] = existingSaga.SagaId,
                ["HotelRequestId"] = existing.Id,
                ["OrderId"] = existing.OrderId,
                ["ProviderBookingCode"] = existing.ProviderBookingCode
            });

            _logger.LogInformation(
                "Hotel request idempotency replayed. HotelRequestId={HotelRequestId}, Status={Status}",
                existing.Id,
                existing.Status);

            return new CreateHotelRequestResponse(
                existing.Id,
                existing.Status.ToString(),
                existing.ExpireAt,
                existing.TotalPrice,
                existing.Currency);
        }

        var now = DateTime.UtcNow;
        var hotelRequest = HotelRequest.Create(
            request.UserId,
            request.ProviderName,
            request.ProviderHotelId,
            request.ProviderRoomId,
            request.SearchCriteriaSnapshot,
            request.SelectedHotelSnapshot,
            request.SelectedRoomSnapshot,
            request.TotalPrice,
            request.Currency,
            request.Breakdown,
            request.Fees,
            request.GuestInfoSnapshot,
            now,
            now.Add(RequestTtl),
            idempotencyKey);

        await _repository.AddAsync(hotelRequest, cancellationToken);
        var saga = HotelBookingSagaState.Start(hotelRequest.UserId, hotelRequest.Id, now);
        await _sagaRepository.AddAsync(saga, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = hotelRequest.UserId,
            ["SagaId"] = saga.SagaId,
            ["HotelRequestId"] = hotelRequest.Id,
            ["OrderId"] = null,
            ["ProviderBookingCode"] = null
        });

        _logger.LogInformation(
            "Hotel request created. HotelRequestId={HotelRequestId}, SagaId={SagaId}, Provider={Provider}",
            hotelRequest.Id,
            saga.SagaId,
            hotelRequest.ProviderName);

        return new CreateHotelRequestResponse(
            hotelRequest.Id,
            hotelRequest.Status.ToString(),
            hotelRequest.ExpireAt,
            hotelRequest.TotalPrice,
            hotelRequest.Currency);
    }

    private static string NormalizeIdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("هدر Idempotency-Key الزامی است");

        return value.Trim();
    }
}
