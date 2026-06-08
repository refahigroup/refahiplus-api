using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Services.Bookings.PrepareOrder;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using System.Text.Json;

namespace Refahi.Modules.Hotels.Application.Bookings.PrepareOrder;

public sealed class PrepareHotelOrderCommandHandler
    : IRequestHandler<PrepareHotelOrderCommand, PrepareHotelOrderResponse>
{
    private readonly IBookingRepository _repository;
    private readonly IMediator _mediator;

    public PrepareHotelOrderCommandHandler(IBookingRepository repository, IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async Task<PrepareHotelOrderResponse> Handle(
        PrepareHotelOrderCommand request,
        CancellationToken cancellationToken)
    {
        var booking = await _repository.GetAsync(new BookingId(request.BookingId), cancellationToken)
            ?? throw new InvalidOperationException("Booking not found.");

        if (booking.Status is BookingStatus.Expired or BookingStatus.PaymentFailed)
            throw new InvalidOperationException("Booking is not payable.");

        if (booking.Status == BookingStatus.Provisional)
        {
            booking.MarkPaymentPending(DateTime.UtcNow);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        var metadataJson = JsonSerializer.Serialize(new
        {
            provider = booking.Provider.ToString(),
            provider_booking_code = booking.ProviderBookingCode.Value,
            hotel_id = booking.ProviderHotelId.Value,
            room_id = booking.ProviderRoomId.Value,
            check_in = booking.StayRange.CheckIn.ToString("yyyy-MM-dd"),
            check_out = booking.StayRange.CheckOut.ToString("yyyy-MM-dd"),
            rooms_count = booking.RoomsCount,
            board_type = booking.BoardType.ToString(),
            guests_count = booking.Guests.Count
        });

        var title = $"رزرو هتل {booking.ProviderHotelId.Value} - اتاق {booking.ProviderRoomId.Value}";
        var orderResult = await _mediator.Send(new CreateOrderCommand(
            UserId: request.UserId,
            SourceModule: "Hotel",
            SourceReferenceId: booking.Id.Value,
            Items:
            [
                new CreateOrderItemInput(
                    Title: title,
                    UnitPriceMinor: booking.CustomerPrice.Amount,
                    Quantity: 1,
                    DiscountAmountMinor: 0,
                    SourceItemId: booking.Id.Value,
                    CategoryCode: "hotel",
                    Tags: ["hotel"],
                    MetadataJson: metadataJson)
            ],
            IdempotencyKey: $"hotel-order-{request.IdempotencyKey}"),
            cancellationToken);

        return new PrepareHotelOrderResponse(
            booking.Id.Value,
            orderResult.OrderId,
            orderResult.OrderNumber,
            orderResult.FinalAmountMinor,
            "Unpaid");
    }
}
