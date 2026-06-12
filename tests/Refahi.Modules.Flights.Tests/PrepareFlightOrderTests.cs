using MediatR;
using Refahi.Modules.Flights.Application.Features.Bookings.PrepareOrder;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;
using Refahi.Modules.Flights.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class PrepareFlightOrderTests
{
    [Fact]
    public async Task Handle_CreatesOrderWithFlightCategoryCode()
    {
        var now = DateTime.UtcNow;
        var booking = FlightBookingTestFactory.CreateDraft(now);
        booking.MarkProviderBooked(
            new ProviderBookingSnapshot("book-1", "track-1", now.AddMinutes(1), providerTraceId: "trace-1"),
            now.AddMinutes(1));

        var repository = new InMemoryFlightBookingRepository(booking);
        var mediator = new CapturingMediator();
        var handler = new PrepareFlightOrderCommandHandler(repository, mediator);

        var result = await handler.Handle(
            new PrepareFlightOrderCommand(
                booking.Id.Value,
                booking.UserId,
                "User",
                "idem-1"),
            CancellationToken.None);

        var orderCommand = mediator.CreateOrderCommand!;
        var item = Assert.Single(orderCommand.Items);

        Assert.NotNull(mediator.CreateOrderCommand);
        Assert.Equal("Flight", orderCommand.SourceModule);
        Assert.Equal(booking.Id.Value, orderCommand.SourceReferenceId);
        Assert.Equal(FlightBooking.CategoryCode, item.CategoryCode);
        Assert.Equal("flight", item.CategoryCode);
        Assert.Contains("flight", item.Tags!);
        Assert.Equal(1_200_000, item.UnitPriceMinor);
        Assert.Equal(booking.Id.Value, result.BookingId);
        Assert.Equal(CapturingMediator.CreatedOrderId, result.OrderId);
        Assert.Equal("ORD-FLIGHT-1", booking.OrderNumber);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    private sealed class InMemoryFlightBookingRepository : IFlightBookingRepository
    {
        private readonly FlightBooking _booking;

        public InMemoryFlightBookingRepository(FlightBooking booking)
        {
            _booking = booking;
        }

        public int SaveChangesCount { get; private set; }

        public Task<FlightBooking?> GetAsync(FlightBookingId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_booking.Id.Equals(id) ? _booking : null);

        public Task<FlightBooking?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_booking.OrderId == orderId ? _booking : null);

        public Task<FlightBooking?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(_booking.IdempotencyKey == idempotencyKey ? _booking : null);

        public Task<FlightBooking?> GetByProviderBookingIdAsync(
            string providerName,
            string providerBookingId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_booking.Provider.ProviderName == providerName
                && _booking.ProviderBooking?.ProviderBookingId == providerBookingId
                    ? _booking
                    : null);

        public Task AddAsync(FlightBooking booking, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingMediator : IMediator
    {
        public static readonly Guid CreatedOrderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        public CreateOrderCommand? CreateOrderCommand { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object response = request switch
            {
                GetOrdersBySourceQuery => new PaginatedOrdersResponse([], 1, 1, 0, 0),
                CreateOrderCommand command => Capture(command),
                _ => throw new NotSupportedException(request.GetType().FullName)
            };

            return Task.FromResult((TResponse)response);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            Task.CompletedTask;

        private CreateOrderResponse Capture(CreateOrderCommand command)
        {
            CreateOrderCommand = command;
            return new CreateOrderResponse(CreatedOrderId, "ORD-FLIGHT-1", 1_200_000);
        }
    }
}
