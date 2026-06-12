using System.Text.Json;
using MediatR;
using Refahi.Modules.Flights.Application.Features.Bookings;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Enums;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;
using Refahi.Modules.Flights.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;

namespace Refahi.Modules.Flights.Application.Features.Bookings.PrepareOrder;

public sealed class PrepareFlightOrderCommandHandler
    : IRequestHandler<PrepareFlightOrderCommand, PrepareFlightOrderResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IFlightBookingRepository _bookingRepository;
    private readonly IMediator _mediator;

    public PrepareFlightOrderCommandHandler(
        IFlightBookingRepository bookingRepository,
        IMediator mediator)
    {
        _bookingRepository = bookingRepository;
        _mediator = mediator;
    }

    public async Task<PrepareFlightOrderResponse> Handle(
        PrepareFlightOrderCommand request,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetAsync(new FlightBookingId(request.BookingId), cancellationToken)
            ?? throw new InvalidOperationException("رزرو پرواز یافت نشد.");

        EnsureOwner(booking, request.UserId, request.CallerRole);

        if (booking.Status is FlightBookingStatus.Expired
            or FlightBookingStatus.Cancelled
            or FlightBookingStatus.Refunded)
        {
            throw new InvalidOperationException("رزرو پرواز قابل پرداخت نیست.");
        }

        if (booking.ProviderBooking is null)
            throw new InvalidOperationException("رزرو تامین‌کننده برای این پرواز ثبت نشده است.");

        if (booking.OrderId.HasValue)
        {
            var existingOrder = await _mediator.Send(new GetOrderByIdQuery(
                booking.OrderId.Value,
                request.UserId,
                request.CallerRole), cancellationToken);

            if (existingOrder is null)
                throw new InvalidOperationException("سفارش رزرو پرواز یافت نشد.");

            if (booking.Status == FlightBookingStatus.OrderCreated)
            {
                booking.MarkPaymentPending(DateTime.UtcNow);
                await _bookingRepository.SaveChangesAsync(cancellationToken);
            }

            return new PrepareFlightOrderResponse(
                booking.Id.Value,
                existingOrder.Id,
                existingOrder.OrderNumber,
                existingOrder.FinalAmountMinor,
                existingOrder.PaymentState);
        }

        if (booking.Status != FlightBookingStatus.ProviderBooked)
            throw new InvalidOperationException("برای این رزرو امکان ایجاد سفارش وجود ندارد.");

        var sourceOrders = await _mediator.Send(new GetOrdersBySourceQuery(
            "Flight",
            booking.Id.Value,
            PageNumber: 1,
            PageSize: 1), cancellationToken);
        var sourceOrder = sourceOrders.Data.FirstOrDefault();
        if (sourceOrder is not null)
        {
            booking.AttachOrder(sourceOrder.Id, sourceOrder.OrderNumber, DateTime.UtcNow);
            booking.MarkPaymentPending(DateTime.UtcNow);
            await _bookingRepository.SaveChangesAsync(cancellationToken);

            var existingOrder = await _mediator.Send(new GetOrderByIdQuery(
                sourceOrder.Id,
                request.UserId,
                request.CallerRole), cancellationToken);

            return new PrepareFlightOrderResponse(
                booking.Id.Value,
                sourceOrder.Id,
                sourceOrder.OrderNumber,
                sourceOrder.FinalAmountMinor,
                existingOrder?.PaymentState ?? "Unpaid");
        }

        var metadataJson = JsonSerializer.Serialize(new
        {
            provider = booking.Provider.ProviderName,
            provider_caption = booking.Provider.ProviderCaption,
            provider_fare_id = booking.SelectedFare.ProviderFareId,
            provider_book_id = booking.ProviderBooking.ProviderBookingId,
            tracking_code = booking.ProviderBooking.ProviderBookingCaption,
            provider_trace_id = booking.ProviderBooking.ProviderTraceId ?? booking.Provider.ProviderTraceId,
            origin = booking.Segments.OrderBy(segment => segment.Sequence).First().OriginAirportCode,
            destination = booking.Segments.OrderBy(segment => segment.Sequence).Last().DestinationAirportCode,
            passenger_count = booking.Passengers.Count,
            expires_at_utc = booking.ExpiresAtUtc
        }, JsonOptions);

        var title = BuildOrderTitle(booking);
        var orderResult = await _mediator.Send(new CreateOrderCommand(
            UserId: booking.UserId,
            SourceModule: "Flight",
            SourceReferenceId: booking.Id.Value,
            Items:
            [
                new CreateOrderItemInput(
                    Title: title,
                    UnitPriceMinor: booking.FareBreakdown.PayableAmount.Amount,
                    Quantity: 1,
                    DiscountAmountMinor: 0,
                    SourceItemId: booking.Id.Value,
                    CategoryCode: FlightBooking.CategoryCode,
                    Tags: ["flight", booking.Provider.ProviderName],
                    MetadataJson: metadataJson)
            ],
            IdempotencyKey: $"flight-order-{booking.Id.Value}-{request.IdempotencyKey.Trim()}"),
            cancellationToken);

        booking.AttachOrder(orderResult.OrderId, orderResult.OrderNumber, DateTime.UtcNow);
        booking.MarkPaymentPending(DateTime.UtcNow);

        await _bookingRepository.SaveChangesAsync(cancellationToken);

        return new PrepareFlightOrderResponse(
            booking.Id.Value,
            orderResult.OrderId,
            orderResult.OrderNumber,
            orderResult.FinalAmountMinor,
            "Unpaid");
    }

    private static string BuildOrderTitle(FlightBooking booking)
    {
        var orderedSegments = booking.Segments.OrderBy(segment => segment.Sequence).ToList();
        var firstSegment = orderedSegments.First();
        var lastSegment = orderedSegments.Last();

        return $"بلیط پرواز {firstSegment.OriginCaption} به {lastSegment.DestinationCaption}";
    }

    private static void EnsureOwner(FlightBooking booking, Guid userId, string callerRole)
    {
        if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase) && booking.UserId != userId)
            throw new UnauthorizedAccessException("دسترسی به این رزرو مجاز نیست.");
    }
}
