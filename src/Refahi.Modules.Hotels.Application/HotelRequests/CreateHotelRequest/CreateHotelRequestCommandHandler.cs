using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;
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
    private readonly IHotelProviderFactory _providerFactory;

    public CreateHotelRequestCommandHandler(
        IHotelRequestRepository repository,
        IHotelBookingSagaRepository sagaRepository,
        IHotelProviderFactory providerFactory,
        ILogger<CreateHotelRequestCommandHandler> logger
    )
    {
        _repository = repository;
        _sagaRepository = sagaRepository;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task<CreateHotelRequestResponse> Handle(
        CreateHotelRequestCommand request,
        CancellationToken cancellationToken
    )
    {
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        var existing = await _repository.GetByIdempotencyKeyAsync(
            request.UserId,
            idempotencyKey,
            cancellationToken
        );

        if (existing is not null)
        {
            var existingSaga = await _sagaRepository.GetByHotelRequestIdAsync(
                existing.Id,
                cancellationToken
            );
            if (existingSaga is null)
            {
                existingSaga = HotelBookingSagaState.Start(
                    existing.UserId,
                    existing.Id,
                    DateTime.UtcNow
                );
                await _sagaRepository.AddAsync(existingSaga, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);
            }

            using var existingScope = _logger.BeginScope(
                new Dictionary<string, object?>
                {
                    ["UserId"] = existing.UserId,
                    ["SagaId"] = existingSaga.SagaId,
                    ["HotelRequestId"] = existing.Id,
                    ["OrderId"] = existing.OrderId,
                    ["ProviderBookingCode"] = existing.ProviderBookingCode,
                }
            );

            _logger.LogInformation(
                "Hotel request idempotency replayed. HotelRequestId={HotelRequestId}, Status={Status}",
                existing.Id,
                existing.Status
            );

            return new CreateHotelRequestResponse(
                existing.Id,
                existing.Status.ToString(),
                existing.ExpireAt,
                existing.TotalPrice,
                existing.Currency
            );
        }

        var provider = ResolveProvider(request.ProviderName);
        var quote = await provider.QuoteRoomPriceAsync(
            new HotelRoomPriceQuoteRequest(
                request.CityId,
                request.ProviderHotelId,
                request.ProviderRoomId,
                request.CheckIn,
                request.CheckOut,
                request.Adults,
                request.Children,
                request.Rooms
            ),
            cancellationToken
        );

        if (
            quote.HotelId != request.ProviderHotelId
            || quote.RoomId != request.ProviderRoomId
            || quote.OriginalPriceMinor <= 0
            || !string.Equals(quote.Currency, "IRR", StringComparison.OrdinalIgnoreCase)
        )
            throw new InvalidOperationException("اطلاعات قیمت اتاق از تامین‌کننده معتبر نیست.");

        if (quote.OriginalPriceMinor != request.ExpectedTotalPriceMinor)
            throw new HotelPriceChangedException(quote.OriginalPriceMinor);

        var searchCriteriaSnapshot = JsonSerializer.Serialize(
            new
            {
                cityId = request.CityId,
                checkIn = request.CheckIn.ToString("yyyy-MM-dd"),
                checkOut = request.CheckOut.ToString("yyyy-MM-dd"),
                adults = request.Adults,
                children = request.Children,
                rooms = request.Rooms,
            }
        );
        var selectedRoomSnapshot = JsonSerializer.Serialize(
            new
            {
                providerRoomId = request.ProviderRoomId,
                boardType = request.BoardType,
                originalPriceMinor = quote.OriginalPriceMinor,
                currency = quote.Currency,
            }
        );
        var breakdown = JsonSerializer.Serialize(
            new
            {
                originalPriceMinor = quote.OriginalPriceMinor,
                discountAmountMinor = 0,
                finalAmountMinor = quote.OriginalPriceMinor,
                currency = quote.Currency,
                pricingVersion = HotelRequest.CurrentPricingVersion,
            }
        );

        var now = DateTime.UtcNow;
        var hotelRequest = HotelRequest.Create(
            request.UserId,
            request.ProviderName,
            request.ProviderHotelId,
            request.ProviderRoomId,
            searchCriteriaSnapshot,
            request.SelectedHotelSnapshot,
            selectedRoomSnapshot,
            quote.OriginalPriceMinor,
            quote.Currency,
            breakdown,
            request.Fees,
            request.GuestInfoSnapshot,
            now,
            now.Add(RequestTtl),
            idempotencyKey
        );

        await _repository.AddAsync(hotelRequest, cancellationToken);
        var saga = HotelBookingSagaState.Start(hotelRequest.UserId, hotelRequest.Id, now);
        await _sagaRepository.AddAsync(saga, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        using var scope = _logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["UserId"] = hotelRequest.UserId,
                ["SagaId"] = saga.SagaId,
                ["HotelRequestId"] = hotelRequest.Id,
                ["OrderId"] = null,
                ["ProviderBookingCode"] = null,
            }
        );

        _logger.LogInformation(
            "Hotel request created. HotelRequestId={HotelRequestId}, SagaId={SagaId}, Provider={Provider}",
            hotelRequest.Id,
            saga.SagaId,
            hotelRequest.ProviderName
        );

        return new CreateHotelRequestResponse(
            hotelRequest.Id,
            hotelRequest.Status.ToString(),
            hotelRequest.ExpireAt,
            hotelRequest.TotalPrice,
            hotelRequest.Currency
        );
    }

    private static string NormalizeIdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("هدر Idempotency-Key الزامی است");

        return value.Trim();
    }

    private IHotelProvider ResolveProvider(string providerName)
    {
        return Enum.TryParse<HotelProviderType>(providerName, true, out var providerType)
            ? _providerFactory.GetProvider(providerType)
            : throw new InvalidOperationException("تامین‌کننده هتل معتبر نیست.");
    }
}
