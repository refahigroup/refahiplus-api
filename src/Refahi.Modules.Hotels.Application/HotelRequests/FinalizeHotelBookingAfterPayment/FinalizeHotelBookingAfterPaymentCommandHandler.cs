using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Booking;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.FinalizeHotelBookingAfterPayment;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;
using Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg.Enums;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Refahi.Modules.Hotels.Application.HotelRequests.FinalizeHotelBookingAfterPayment;

public sealed class FinalizeHotelBookingAfterPaymentCommandHandler
    : IRequestHandler<FinalizeHotelBookingAfterPaymentCommand>
{
    private const int ProviderConfirmationFailureCompensationThreshold = 10;

    private readonly IHotelRequestRepository _repository;
    private readonly IHotelBookingSagaRepository _sagaRepository;
    private readonly IHotelProviderBookingCacheRepository _providerBookingCacheRepository;
    private readonly IHotelProvider _provider;
    private readonly IMediator _mediator;
    private readonly ILogger<FinalizeHotelBookingAfterPaymentCommandHandler> _logger;

    public FinalizeHotelBookingAfterPaymentCommandHandler(
        IHotelRequestRepository repository,
        IHotelBookingSagaRepository sagaRepository,
        IHotelProviderBookingCacheRepository providerBookingCacheRepository,
        IHotelProvider provider,
        IMediator mediator,
        ILogger<FinalizeHotelBookingAfterPaymentCommandHandler> logger)
    {
        _repository = repository;
        _sagaRepository = sagaRepository;
        _providerBookingCacheRepository = providerBookingCacheRepository;
        _provider = provider;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        FinalizeHotelBookingAfterPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var hotelRequest = await _repository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (hotelRequest is null)
            return Unit.Value;

        var saga = request.SagaId.HasValue
            ? await _sagaRepository.GetAsync(request.SagaId.Value, cancellationToken)
            : await _sagaRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);

        if (saga is null)
        {
            _logger.LogWarning(
                "Hotel saga not found during paid event finalization. OrderId={OrderId}, RequestId={RequestId}",
                request.OrderId,
                hotelRequest.Id);
            return Unit.Value;
        }

        if (saga.HotelRequestId != hotelRequest.Id || saga.UserId != request.UserId)
        {
            _logger.LogWarning(
                "Hotel saga mismatch during paid event finalization. SagaId={SagaId}, OrderId={OrderId}, RequestId={RequestId}",
                saga.SagaId,
                request.OrderId,
                hotelRequest.Id);
            return Unit.Value;
        }

        if (saga.Status is HotelBookingSagaStatus.Compensated or HotelBookingSagaStatus.Failed)
        {
            _logger.LogWarning(
                "Hotel paid event skipped because saga is terminal. SagaId={SagaId}, Status={Status}, OrderId={OrderId}",
                saga.SagaId,
                saga.Status,
                request.OrderId);
            return Unit.Value;
        }

        if (hotelRequest.UserId != request.UserId)
        {
            _logger.LogWarning(
                "Hotel request ownership mismatch during finalization. OrderId={OrderId}, RequestId={RequestId}",
                request.OrderId,
                hotelRequest.Id);
            return Unit.Value;
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = request.UserId,
            ["SagaId"] = saga.SagaId,
            ["HotelRequestId"] = hotelRequest.Id,
            ["OrderId"] = request.OrderId,
            ["ProviderBookingCode"] = hotelRequest.ProviderBookingCode
        });

        var now = DateTime.UtcNow;
        saga.MarkPaid(request.OrderId, now);
        await _repository.SaveChangesAsync(cancellationToken);

        var order = await _mediator.Send(
            new GetOrderByIdQuery(request.OrderId, request.UserId, "User"),
            cancellationToken);

        if (order is null ||
            !order.SourceModule.Equals("Hotel", StringComparison.OrdinalIgnoreCase) ||
            !order.ReferenceType.Equals("HotelRequest", StringComparison.OrdinalIgnoreCase) ||
            !order.PaymentState.Equals("Paid", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Blocked hotel provider booking because order is not paid or not a HotelRequest order. OrderId={OrderId}, RequestId={RequestId}",
                request.OrderId,
                hotelRequest.Id);
            saga.Fail("Paid event did not match a paid HotelRequest order.", DateTime.UtcNow);
            await _repository.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }

        if (!string.IsNullOrWhiteSpace(hotelRequest.ProviderBookingCode) ||
            saga.Status is HotelBookingSagaStatus.ProviderBookingConfirmed or HotelBookingSagaStatus.Completed)
        {
            if (saga.Status == HotelBookingSagaStatus.Paid)
                saga.MarkProviderBookingStarted(DateTime.UtcNow);
            saga.MarkProviderBookingConfirmed(DateTime.UtcNow);
            saga.Complete(DateTime.UtcNow);
            if (!string.IsNullOrWhiteSpace(hotelRequest.ProviderBookingCode))
                hotelRequest.Complete(DateTime.UtcNow);
            await _repository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Hotel paid event replay completed from existing provider state. SagaId={SagaId}, OrderId={OrderId}, HotelRequestId={HotelRequestId}, ProviderBookingCode={ProviderBookingCode}",
                saga.SagaId,
                request.OrderId,
                hotelRequest.Id,
                hotelRequest.ProviderBookingCode);
            return Unit.Value;
        }

        if (saga.Status == HotelBookingSagaStatus.ProviderBookingStarted)
        {
            _logger.LogWarning(
                "Retrying hotel provider booking because saga is already started. SagaId={SagaId}, OrderId={OrderId}, RequestId={RequestId}",
                saga.SagaId,
                request.OrderId,
                hotelRequest.Id);
        }

        var draft = BuildProviderDraft(hotelRequest);
        draft.IdempotencyKey = saga.SagaId.ToString("N");
        var requestHash = ComputeRequestHash(draft);

        _logger.LogInformation(
            "Finalizing hotel request after order payment. SagaId={SagaId}, OrderId={OrderId}, RequestId={RequestId}, Provider={Provider}, ProviderIdempotencyKey={ProviderIdempotencyKey}",
            saga.SagaId,
            request.OrderId,
            hotelRequest.Id,
            hotelRequest.ProviderName,
            draft.IdempotencyKey);

        saga.MarkProviderBookingStarted(DateTime.UtcNow);
        var cacheEntry = await GetOrCreateProviderCacheEntryAsync(
            hotelRequest.ProviderName,
            draft.IdempotencyKey,
            requestHash,
            saga.SagaId,
            hotelRequest.Id,
            cancellationToken);
        cacheEntry.EnsureSameRequest(requestHash);
        cacheEntry.MarkAttemptStarted(DateTime.UtcNow);
        await _providerBookingCacheRepository.SaveChangesAsync(cancellationToken);

        try
        {
            var providerBookingCode = cacheEntry.ProviderBookingCode;
            if (string.IsNullOrWhiteSpace(providerBookingCode))
            {
                var providerResult = await _provider.CreateBookingAsync(draft);
                providerBookingCode = providerResult.BookingCode;
                cacheEntry.MarkCompleted(
                    providerResult.BookingCode,
                    JsonSerializer.Serialize(providerResult),
                    DateTime.UtcNow);
                await _providerBookingCacheRepository.SaveChangesAsync(cancellationToken);
            }

            await ConfirmOrReconcileProviderBookingAsync(providerBookingCode, cancellationToken);

            var confirmedAt = DateTime.UtcNow;
            hotelRequest.MarkProviderConfirmed(providerBookingCode, confirmedAt);
            saga.MarkProviderBookingConfirmed(confirmedAt);
            hotelRequest.Complete(confirmedAt);
            saga.Complete(confirmedAt);
            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Hotel booking completed after payment. SagaId={SagaId}, OrderId={OrderId}, HotelRequestId={HotelRequestId}, ProviderBookingCode={ProviderBookingCode}",
                saga.SagaId,
                request.OrderId,
                hotelRequest.Id,
                providerBookingCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hotel provider booking failed after payment. SagaId={SagaId}, OrderId={OrderId}, RequestId={RequestId}",
                saga.SagaId,
                request.OrderId,
                hotelRequest.Id);

            if (string.IsNullOrWhiteSpace(cacheEntry.ProviderBookingCode))
            {
                cacheEntry.MarkFailed("Provider booking failed before booking code was issued.", DateTime.UtcNow);
                await ExecuteCompensationAsync(
                    saga,
                    hotelRequest,
                    request.OrderId,
                    "Provider booking failed after payment.",
                    cancellationToken);
                return Unit.Value;
            }

            if (cacheEntry.AttemptCount >= ProviderConfirmationFailureCompensationThreshold)
            {
                cacheEntry.MarkFailed(
                    "Provider booking was created but confirmation failed after the retry threshold.",
                    DateTime.UtcNow);
                await _providerBookingCacheRepository.SaveChangesAsync(cancellationToken);

                await ExecuteCompensationAsync(
                    saga,
                    hotelRequest,
                    request.OrderId,
                    "Provider booking confirmation failed after retry threshold; external cancellation required.",
                    cancellationToken);

                return Unit.Value;
            }

            saga.RecordRecoverableFailure("Provider booking created but confirmation failed; reconciliation will retry.", DateTime.UtcNow);
            await _repository.SaveChangesAsync(cancellationToken);
            throw;
        }

        return Unit.Value;
    }

    private async Task<HotelProviderBookingCacheEntry> GetOrCreateProviderCacheEntryAsync(
        string providerName,
        string idempotencyKey,
        string requestHash,
        Guid sagaId,
        Guid hotelRequestId,
        CancellationToken cancellationToken)
    {
        var cacheEntry = await _providerBookingCacheRepository.GetAsync(
            providerName,
            idempotencyKey,
            cancellationToken);

        if (cacheEntry is not null)
            return cacheEntry;

        cacheEntry = HotelProviderBookingCacheEntry.Create(
            providerName,
            idempotencyKey,
            requestHash,
            sagaId,
            hotelRequestId,
            DateTime.UtcNow);

        await _providerBookingCacheRepository.AddAsync(cacheEntry, cancellationToken);
        return cacheEntry;
    }

    private async Task ConfirmOrReconcileProviderBookingAsync(
        string providerBookingCode,
        CancellationToken cancellationToken)
    {
        BookingStatusDto? status = null;
        try
        {
            status = await _provider.GetBookingStatusAsync(providerBookingCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Hotel provider status lookup failed before confirmation. ProviderBookingCode={ProviderBookingCode}",
                providerBookingCode);
        }

        if (status is not null &&
            status.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _provider.ConfirmBookingAsync(providerBookingCode);
    }

    private async Task ExecuteCompensationAsync(
        Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.HotelBookingSagaState saga,
        Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.HotelRequest hotelRequest,
        Guid orderId,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var cancelResult = await _mediator.Send(new CancelOrderCommand(
                orderId,
                reason,
                $"hotel-saga-compensation-{saga.SagaId:N}"),
                cancellationToken);

            hotelRequest.MarkFailed(DateTime.UtcNow);
            saga.Compensate($"{reason} Order compensation action: {cancelResult.PaymentAction}.", DateTime.UtcNow);

            _logger.LogWarning(
                "Hotel saga compensated after provider failure. SagaId={SagaId}, OrderId={OrderId}, PaymentAction={PaymentAction}",
                saga.SagaId,
                orderId,
                cancelResult.PaymentAction);

            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception compensationException)
        {
            saga.RecordRecoverableFailure(
                $"Provider failed and compensation failed: {compensationException.Message}",
                DateTime.UtcNow);
            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                compensationException,
                "Hotel compensation failed after provider booking failure. SagaId={SagaId}, OrderId={OrderId}",
                saga.SagaId,
                orderId);

            throw;
        }
    }

    private static string ComputeRequestHash(BookingDraftDto draft)
    {
        var payload = JsonSerializer.Serialize(new
        {
            draft.HotelId,
            draft.RoomId,
            draft.CheckIn,
            draft.CheckOut,
            draft.RoomsCount,
            draft.BoardType,
            draft.Email,
            draft.Phone,
            Guests = draft.Guests
                .Select(g => new { g.FullName, g.Age, g.Type })
                .ToArray()
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private static BookingDraftDto BuildProviderDraft(Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.HotelRequest request)
    {
        using var search = JsonDocument.Parse(request.SearchCriteriaSnapshot);
        using var room = JsonDocument.Parse(request.SelectedRoomSnapshot);
        using var guests = JsonDocument.Parse(request.GuestInfoSnapshot);

        var root = guests.RootElement;
        var guestItems = root.TryGetProperty("guests", out var guestsElement) &&
            guestsElement.ValueKind == JsonValueKind.Array
            ? guestsElement.EnumerateArray().Select(ReadGuest).ToList()
            : [];

        return new BookingDraftDto
        {
            HotelId = request.ProviderHotelId,
            RoomId = request.ProviderRoomId,
            CheckIn = ReadDateOnly(search.RootElement, "checkIn")
                ?? ReadDateOnly(search.RootElement, "check_in")
                ?? throw new InvalidOperationException("تاریخ ورود درخواست هتل معتبر نیست"),
            CheckOut = ReadDateOnly(search.RootElement, "checkOut")
                ?? ReadDateOnly(search.RootElement, "check_out")
                ?? throw new InvalidOperationException("تاریخ خروج درخواست هتل معتبر نیست"),
            RoomsCount = ReadInt(search.RootElement, "rooms") ?? ReadInt(search.RootElement, "roomsCount") ?? 1,
            BoardType = ReadString(room.RootElement, "boardType")
                ?? ReadString(room.RootElement, "board_type")
                ?? "BedBreakfast",
            Guests = guestItems,
            Email = ReadString(root, "email"),
            Phone = ReadString(root, "phone")
        };
    }

    private static GuestDto ReadGuest(JsonElement element)
        => new()
        {
            FullName = ReadString(element, "fullName") ?? ReadString(element, "full_name") ?? "",
            Age = ReadInt(element, "age") ?? 30,
            Type = ReadString(element, "type") ?? "Adult"
        };

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }

    private static DateOnly? ReadDateOnly(JsonElement element, string propertyName)
    {
        var value = ReadString(element, propertyName);
        return DateOnly.TryParse(value, out var parsed) ? parsed : null;
    }
}
