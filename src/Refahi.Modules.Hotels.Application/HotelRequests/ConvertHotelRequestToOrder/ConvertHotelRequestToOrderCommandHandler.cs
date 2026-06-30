using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.ConvertHotelRequestToOrder;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.Enums;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using System.Text.Json;

namespace Refahi.Modules.Hotels.Application.HotelRequests.ConvertHotelRequestToOrder;

public sealed class ConvertHotelRequestToOrderCommandHandler
    : IRequestHandler<ConvertHotelRequestToOrderCommand, ConvertHotelRequestToOrderResponse>
{
    private readonly IHotelRequestRepository _repository;
    private readonly IHotelBookingSagaRepository _sagaRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<ConvertHotelRequestToOrderCommandHandler> _logger;

    public ConvertHotelRequestToOrderCommandHandler(
        IHotelRequestRepository repository,
        IHotelBookingSagaRepository sagaRepository,
        IMediator mediator,
        ILogger<ConvertHotelRequestToOrderCommandHandler> logger)
    {
        _repository = repository;
        _sagaRepository = sagaRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ConvertHotelRequestToOrderResponse> Handle(
        ConvertHotelRequestToOrderCommand request,
        CancellationToken cancellationToken)
    {
        var hotelRequest = await _repository.GetForUserAsync(request.RequestId, request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("درخواست هتل یافت نشد");

        var now = DateTime.UtcNow;
        var saga = await _sagaRepository.GetByHotelRequestIdAsync(hotelRequest.Id, cancellationToken);
        if (saga is null)
        {
            saga = HotelBookingSagaState.Start(hotelRequest.UserId, hotelRequest.Id, now);
            await _sagaRepository.AddAsync(saga, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = hotelRequest.UserId,
            ["SagaId"] = saga.SagaId,
            ["HotelRequestId"] = hotelRequest.Id,
            ["OrderId"] = hotelRequest.OrderId,
            ["ProviderBookingCode"] = hotelRequest.ProviderBookingCode
        });

        if (hotelRequest.Status == HotelRequestStatus.Created && hotelRequest.ExpireAt <= now)
        {
            hotelRequest.MarkExpired(now);
            saga.Fail("Hotel request expired before order creation.", now);
            await _repository.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("مهلت درخواست هتل به پایان رسیده است");
        }

        if (hotelRequest.Status == HotelRequestStatus.Expired)
            throw new InvalidOperationException("مهلت درخواست هتل به پایان رسیده است");

        if (hotelRequest.Status == HotelRequestStatus.Cancelled)
            throw new InvalidOperationException("درخواست هتل لغو شده است");

        if (hotelRequest.Status == HotelRequestStatus.Failed)
            throw new InvalidOperationException("درخواست هتل ناموفق شده است");

        if (hotelRequest.OrderId.HasValue)
        {
            saga.MarkOrderCreated(hotelRequest.OrderId.Value, now);
            if (hotelRequest.Status == HotelRequestStatus.ConvertedToOrder)
                saga.MarkPaymentPending(now);
            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Hotel request order conversion replayed. SagaId={SagaId}, HotelRequestId={HotelRequestId}, OrderId={OrderId}",
                saga.SagaId,
                hotelRequest.Id,
                hotelRequest.OrderId.Value);

            return new ConvertHotelRequestToOrderResponse(
                hotelRequest.Id,
                hotelRequest.OrderId.Value,
                null,
                hotelRequest.TotalPrice,
                hotelRequest.Status.ToString());
        }

        CreateOrderResponse order;
        try
        {
            order = await _mediator.Send(new CreateOrderCommand(
                UserId: hotelRequest.UserId,
                SourceModule: "Hotel",
                SourceReferenceId: hotelRequest.Id,
                Items:
                [
                    new CreateOrderItemInput(
                        Title: BuildOrderTitle(hotelRequest),
                        UnitPriceMinor: hotelRequest.TotalPrice,
                        Quantity: 1,
                        DiscountAmountMinor: 0,
                        SourceItemId: hotelRequest.Id,
                        CategoryCode: "hotel",
                        Tags: ["hotel", "hotel-request"],
                        MetadataJson: BuildMetadataJson(hotelRequest))
                ],
                IdempotencyKey: $"hotel-request-order-{NormalizeIdempotencyKey(request.IdempotencyKey)}",
                ReferenceType: "HotelRequest",
                SagaId: saga.SagaId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hotel order creation failed. SagaId={SagaId}, RequestId={RequestId}",
                saga.SagaId,
                hotelRequest.Id);

            hotelRequest.MarkFailed(DateTime.UtcNow);
            saga.Fail("Order creation failed.", DateTime.UtcNow);
            await _repository.SaveChangesAsync(cancellationToken);
            throw;
        }

        now = DateTime.UtcNow;
        hotelRequest.ConvertToOrder(order.OrderId, now);
        saga.MarkOrderCreated(order.OrderId, now);
        saga.MarkPaymentPending(now);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Hotel request converted to order. SagaId={SagaId}, HotelRequestId={HotelRequestId}, OrderId={OrderId}",
            saga.SagaId,
            hotelRequest.Id,
            order.OrderId);

        return new ConvertHotelRequestToOrderResponse(
            hotelRequest.Id,
            order.OrderId,
            order.OrderNumber,
            order.FinalAmountMinor,
            hotelRequest.Status.ToString());
    }

    private static string BuildOrderTitle(Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.HotelRequest request)
        => $"رزرو هتل {request.ProviderHotelId} - اتاق {request.ProviderRoomId}";

    private static string BuildMetadataJson(Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.HotelRequest request)
        => JsonSerializer.Serialize(new
        {
            reference_type = "HotelRequest",
            request_id = request.Id,
            provider = request.ProviderName,
            provider_hotel_id = request.ProviderHotelId,
            provider_room_id = request.ProviderRoomId,
            search_criteria_snapshot = request.SearchCriteriaSnapshot,
            selected_hotel_snapshot = request.SelectedHotelSnapshot,
            selected_room_snapshot = request.SelectedRoomSnapshot,
            breakdown = request.Breakdown,
            fees = request.Fees,
            guest_info_snapshot = request.GuestInfoSnapshot
        });

    private static string NormalizeIdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("هدر Idempotency-Key الزامی است");

        return value.Trim();
    }
}
