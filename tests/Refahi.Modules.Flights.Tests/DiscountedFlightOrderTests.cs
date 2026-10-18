using Xunit;
using System.Reflection;
using MediatR;
using Refahi.Modules.Flights.Application.Contracts.Providers;
using Refahi.Modules.Flights.Application.Features.Bookings.IssueTicket;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;
using Refahi.Modules.Flights.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
namespace Refahi.Modules.Flights.Tests;

public sealed class DiscountedFlightOrderTests
{
    [Theory]
    [InlineData(1_200_000, true)]
    [InlineData(1_100_000, false)]
    public async Task Issuance_ValidatesServiceAmount_WhileCustomerPaysDiscountedAmount(long sourceAmount, bool valid)
    {
        var now = DateTime.UtcNow; var booking = FlightBookingTestFactory.CreateDraft(now);
        booking.MarkProviderBooked(new ProviderBookingSnapshot("booking", "رزرو", now), now);
        var orderId = Guid.NewGuid(); booking.AttachOrder(orderId, "test", now);
        var order = new OrderDto(orderId, "test", booking.UserId, 1_200_000, 0, 0, "masked", 100_000, 1_100_000,
            "Confirmed", "Paid", "Flight", booking.Id.Value, "FlightBooking", null, null, null, 0, [], DateTimeOffset.UtcNow, SourceAmountMinor: sourceAmount);
        var repo = FlightTestProxy<IFlightBookingRepository>.Make((method, _) => method.Name == "GetAsync" ? Task.FromResult<FlightBooking?>(booking) : Task.CompletedTask);
        var mediator = FlightTestProxy<IMediator>.Make((method, _) => method.Name == "Send" ? Task.FromResult<OrderDto?>(order) : throw new NotSupportedException());
        var handler = new IssueFlightTicketCommandHandler(repo, new ProviderBoundary(), mediator);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new(booking.Id.Value, booking.UserId, "User", "issue"), default));
        if (valid) Assert.Equal("provider-boundary", exception.Message);
        else Assert.Contains("مبلغ", exception.Message);
    }
    private sealed class ProviderBoundary : IFlightProviderFactory
    {
        public IFlightProvider GetProvider(FlightProviderType providerType) => throw new InvalidOperationException("provider-boundary");
        public IFlightProvider GetDefaultProvider() => throw new InvalidOperationException("provider-boundary");
    }
}
public class FlightTestProxy<T> : DispatchProxy where T : class
{
    private Func<MethodInfo, object?[]?, object?> _invoke = null!;
    public static T Make(Func<MethodInfo, object?[]?, object?> invoke)
    { var value = Create<T, FlightTestProxy<T>>(); ((FlightTestProxy<T>)(object)value)._invoke = invoke; return value; }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _invoke(targetMethod!, args);
}
