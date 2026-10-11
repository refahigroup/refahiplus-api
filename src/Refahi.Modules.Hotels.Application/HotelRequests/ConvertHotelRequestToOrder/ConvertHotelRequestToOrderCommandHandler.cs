using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.ConvertHotelRequestToOrder;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.Enums;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Hotels.Application.HotelRequests.ConvertHotelRequestToOrder;

public sealed class ConvertHotelRequestToOrderCommandHandler
    : IRequestHandler<ConvertHotelRequestToOrderCommand, ConvertHotelRequestToOrderResponse>
{
    private readonly IHotelRequestRepository _repository;
    private readonly IHotelBookingSagaRepository _sagaRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<ConvertHotelRequestToOrderCommandHandler> _logger;
    private readonly IHotelCatalogueRepository? _catalogue;

    public ConvertHotelRequestToOrderCommandHandler(
        IHotelRequestRepository repository,
        IHotelBookingSagaRepository sagaRepository,
        IMediator mediator,
        ILogger<ConvertHotelRequestToOrderCommandHandler> logger,
        IHotelCatalogueRepository? catalogue = null
    )
    {
        _repository = repository;
        _sagaRepository = sagaRepository;
        _mediator = mediator;
        _logger = logger;
        _catalogue = catalogue;
    }

    public async Task<ConvertHotelRequestToOrderResponse> Handle(
        ConvertHotelRequestToOrderCommand request,
        CancellationToken cancellationToken
    )
    {
        var hotelRequest =
            await _repository.GetForUserAsync(request.RequestId, request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("درخواست هتل یافت نشد");

        var now = DateTime.UtcNow;
        if (hotelRequest.ProviderName == "Lamasoo")
        {
            var reservation = _catalogue is null ? null : await _catalogue.ReservationAsync(hotelRequest.Id, cancellationToken);
            if (reservation?.State != Refahi.Modules.Hotels.Domain.Aggregates.Catalogue.HotelExternalState.Reserved)
                throw new InvalidOperationException("وضعیت رزرو اجازه ایجاد سفارش نمی‌دهد.");
        }

        if (hotelRequest.PricingVersion != HotelRequest.CurrentPricingVersion)
            throw new InvalidOperationException(
                "قیمت این درخواست هتل منقضی شده است؛ لطفاً اتاق را دوباره انتخاب کنید"
            );

        var saga = await _sagaRepository.GetByHotelRequestIdAsync(
            hotelRequest.Id,
            cancellationToken
        );
        if (saga is null)
        {
            saga = HotelBookingSagaState.Start(hotelRequest.UserId, hotelRequest.Id, now);
            await _sagaRepository.AddAsync(saga, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        using var scope = _logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["UserId"] = hotelRequest.UserId,
                ["SagaId"] = saga.SagaId,
                ["HotelRequestId"] = hotelRequest.Id,
                ["OrderId"] = hotelRequest.OrderId,
                ["ProviderBookingCode"] = hotelRequest.ProviderBookingCode,
            }
        );

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
                hotelRequest.OrderId.Value
            );

            return new ConvertHotelRequestToOrderResponse(
                hotelRequest.Id,
                hotelRequest.OrderId.Value,
                null,
                hotelRequest.TotalPrice,
                hotelRequest.Status.ToString()
            );
        }

        CreateOrderResponse order;
        try
        {
            order = await _mediator.Send(
                new CreateOrderCommand(
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
                            MetadataJson: BuildMetadataJson(hotelRequest)
                        ),
                    ],
                    IdempotencyKey: $"hotel-request-order-{hotelRequest.Id:N}",
                    ReferenceType: "HotelRequest",
                    SagaId: saga.SagaId,
                    PayableUntil: new DateTimeOffset(hotelRequest.ExpireAt, TimeSpan.Zero)
                ),
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hotel order creation failed. SagaId={SagaId}, RequestId={RequestId}",
                saga.SagaId,
                hotelRequest.Id
            );

            saga.RecordRecoverableFailure("Order creation needs recovery with the stable request key.", DateTime.UtcNow);
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
            order.OrderId
        );

        return new ConvertHotelRequestToOrderResponse(
            hotelRequest.Id,
            order.OrderId,
            order.OrderNumber,
            order.FinalAmountMinor,
            hotelRequest.Status.ToString()
        );
    }

    private static string BuildOrderTitle(
        Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.HotelRequest request
    ) => $"رزرو هتل {ReadName(request.SelectedHotelSnapshot)} - اتاق {ReadName(request.SelectedRoomSnapshot)}";

    private static string ReadName(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("name", out var name) ? name.GetString() ?? "انتخابی" : "انتخابی";
    }

    private static string BuildMetadataJson(
        Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.HotelRequest request
    ) =>
        JsonSerializer.Serialize(
            new
            {
                reference_type = "HotelRequest",
                request_id = request.Id,
                provider = request.ProviderName,
                provider_hotel_id = request.ProviderHotelId,
                provider_room_id = request.ProviderRoomId,
                native_hotel_id = request.NativeHotelId,
                native_room_id = request.NativeRoomId,
                offer_token = request.OfferToken,
                search_criteria_snapshot = request.SearchCriteriaSnapshot,
                selected_hotel_snapshot = request.SelectedHotelSnapshot,
                selected_room_snapshot = request.SelectedRoomSnapshot,
                breakdown = request.Breakdown,
                fees = request.Fees,
                guest_info_snapshot = request.GuestInfoSnapshot,
            }
        );

}
