using System.Text.Json;
using MediatR;
using Refahi.Modules.Flights.Application.Contracts.Providers;
using Refahi.Modules.Flights.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Flights.Application.Features.Bookings;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Entities;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Enums;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;
using Refahi.Modules.Flights.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;

namespace Refahi.Modules.Flights.Application.Features.Bookings.IssueTicket;

public sealed class IssueFlightTicketCommandHandler
    : IRequestHandler<IssueFlightTicketCommand, IssueFlightTicketResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IFlightBookingRepository _bookingRepository;
    private readonly IFlightProviderFactory _providerFactory;
    private readonly IMediator _mediator;

    public IssueFlightTicketCommandHandler(
        IFlightBookingRepository bookingRepository,
        IFlightProviderFactory providerFactory,
        IMediator mediator)
    {
        _bookingRepository = bookingRepository;
        _providerFactory = providerFactory;
        _mediator = mediator;
    }

    public async Task<IssueFlightTicketResponse> Handle(
        IssueFlightTicketCommand request,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetAsync(new FlightBookingId(request.BookingId), cancellationToken)
            ?? throw new InvalidOperationException("رزرو پرواز یافت نشد.");

        EnsureOwner(booking, request.UserId, request.CallerRole);

        if (booking.Status == FlightBookingStatus.Issued)
        {
            return ToResponse(booking);
        }

        if (booking.ProviderBooking is null || string.IsNullOrWhiteSpace(booking.ProviderBooking.ProviderBookingId))
            throw new InvalidOperationException("شناسه رزرو تامین‌کننده برای صدور بلیط موجود نیست.");

        if (!booking.OrderId.HasValue)
            throw new InvalidOperationException("برای این رزرو سفارش ثبت نشده است.");

        var order = await _mediator.Send(new GetOrderByIdQuery(
            booking.OrderId.Value,
            request.UserId,
            request.CallerRole), cancellationToken)
            ?? throw new InvalidOperationException("سفارش رزرو پرواز یافت نشد.");

        ValidatePaidOrder(booking, order);

        MarkBookingPaidIfNeeded(booking);

        var provider = ResolveProvider(booking.Provider.ProviderName);
        var bookId = booking.ProviderBooking.ProviderBookingId;

        var preIssueInquiry = await provider.InquiryAsync(new FlightInquiryRequest(bookId), cancellationToken);
        if (TryMarkIssuedFromInquiry(booking, preIssueInquiry, DateTime.UtcNow))
        {
            await _bookingRepository.SaveChangesAsync(cancellationToken);
            return ToResponse(booking);
        }

        if (booking.Status != FlightBookingStatus.Issuing)
            booking.StartIssuing(DateTime.UtcNow);

        await _bookingRepository.SaveChangesAsync(cancellationToken);

        var issueResponse = await provider.IssueAsync(new FlightIssueRequest(bookId), cancellationToken);
        var postIssueInquiry = await provider.InquiryAsync(new FlightInquiryRequest(bookId), cancellationToken);

        if (TryMarkIssuedFromInquiry(booking, postIssueInquiry, DateTime.UtcNow))
        {
            await _bookingRepository.SaveChangesAsync(cancellationToken);
            return ToResponse(booking);
        }

        booking.MarkIssueFailed(
            $"صدور بلیط توسط تامین‌کننده تایید نشد. وضعیت: {issueResponse.Status ?? postIssueInquiry.Status ?? "نامشخص"}",
            DateTime.UtcNow);

        await _bookingRepository.SaveChangesAsync(cancellationToken);

        throw new InvalidOperationException("صدور بلیط پرواز ناموفق بود.");
    }

    private IFlightProvider ResolveProvider(string providerName)
    {
        return Enum.TryParse<FlightProviderType>(providerName, ignoreCase: true, out var providerType)
            ? _providerFactory.GetProvider(providerType)
            : _providerFactory.GetDefaultProvider();
    }

    private static bool TryMarkIssuedFromInquiry(
        FlightBooking booking,
        FlightInquiryResponse inquiry,
        DateTime nowUtc)
    {
        if (inquiry.Tickets.Count == 0)
            return false;

        var passengers = booking.Passengers.ToList();
        if (inquiry.Tickets.Count != passengers.Count)
            return false;

        if (booking.Status is FlightBookingStatus.PaymentPending or FlightBookingStatus.OrderCreated)
            MarkBookingPaidIfNeeded(booking);

        if (booking.Status != FlightBookingStatus.Issuing)
            booking.StartIssuing(nowUtc);

        var tickets = inquiry.Tickets
            .Select((ticket, index) =>
            {
                var passenger = passengers[index];
                var ticketNumber = ticket.Serial ?? ticket.DocumentId ?? ticket.Pnr;
                if (string.IsNullOrWhiteSpace(ticketNumber))
                    throw new InvalidOperationException("شماره بلیط تامین‌کننده معتبر نیست.");

                return new IssuedTicket(
                    passenger.Id,
                    ticketNumber,
                    ticket.PassengerName ?? passenger.DisplayName,
                    nowUtc,
                    ticket.Serial,
                    inquiry.ProviderTraceId,
                    JsonSerializer.Serialize(new
                    {
                        ticket.Serial,
                        ticket.Pnr,
                        ticket.PassengerName,
                        ticket.PassengerType,
                        ticket.Direction,
                        ticket.DocumentType,
                        ticket.DocumentId,
                        inquiry.ProviderTraceId,
                        inquiry.RawPayloadSnapshot
                    }, JsonOptions));
            })
            .ToList();

        booking.MarkIssued(tickets, nowUtc);
        return true;
    }

    private static void MarkBookingPaidIfNeeded(FlightBooking booking)
    {
        if (booking.Status == FlightBookingStatus.OrderCreated)
            booking.MarkPaymentPending(DateTime.UtcNow);

        if (booking.Status == FlightBookingStatus.PaymentPending)
            booking.MarkPaid(DateTime.UtcNow);
    }

    private static void ValidatePaidOrder(FlightBooking booking, OrderDto order)
    {
        if (!string.Equals(order.SourceModule, "Flight", StringComparison.OrdinalIgnoreCase)
            || order.SourceReferenceId != booking.Id.Value)
        {
            throw new InvalidOperationException("سفارش با رزرو پرواز همخوانی ندارد.");
        }

        if (order.UserId != booking.UserId)
            throw new InvalidOperationException("مالک سفارش با مالک رزرو پرواز همخوانی ندارد.");

        if (order.FinalAmountMinor != booking.FareBreakdown.PayableAmount.Amount)
            throw new InvalidOperationException("مبلغ سفارش با مبلغ رزرو پرواز همخوانی ندارد.");

        if (!string.Equals(order.PaymentState, "Paid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("سفارش رزرو پرواز پرداخت نشده است.");
    }

    private static IssueFlightTicketResponse ToResponse(FlightBooking booking)
    {
        return new IssueFlightTicketResponse(
            booking.Id.Value,
            booking.OrderId!.Value,
            booking.Status.ToString(),
            FlightBookingDtoMapper.ToTicketDtos(booking));
    }

    private static void EnsureOwner(FlightBooking booking, Guid userId, string callerRole)
    {
        if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase) && booking.UserId != userId)
            throw new UnauthorizedAccessException("دسترسی به این رزرو مجاز نیست.");
    }
}
