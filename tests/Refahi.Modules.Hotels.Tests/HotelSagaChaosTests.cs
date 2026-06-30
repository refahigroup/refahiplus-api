using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Account;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Booking;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Hotel;
using Refahi.Modules.Hotels.Application.Contracts.Providers.Queries;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CancelProviderBooking;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.FinalizeHotelBookingAfterPayment;
using Refahi.Modules.Hotels.Application.Contracts.Services.Statics.Cities;
using Refahi.Modules.Hotels.Application.HotelRequests.CancelProviderBooking;
using Refahi.Modules.Hotels.Application.HotelRequests.FinalizeHotelBookingAfterPayment;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.Enums;
using Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Xunit;

namespace Refahi.Modules.Hotels.Tests;

public sealed class HotelSagaChaosTests
{
    [Fact]
    public async Task Finalize_WhenProviderCreateFailsAfterPayment_RefundsOrderAndCompensatesSaga()
    {
        var fixture = HotelSagaFixture.CreatePaid();
        var provider = new FakeHotelProvider { CreateFailure = new InvalidOperationException("provider down") };
        var mediator = new CapturingMediator(fixture.Request, "Paid");
        var handler = fixture.CreateHandler(provider, mediator);

        await handler.Handle(new FinalizeHotelBookingAfterPaymentCommand(
            fixture.Request.OrderId!.Value,
            fixture.Request.UserId,
            Guid.NewGuid(),
            fixture.Saga.SagaId), CancellationToken.None);

        Assert.Equal(1, provider.CreateCount);
        Assert.NotNull(mediator.CancelOrderCommand);
        Assert.Equal(HotelBookingSagaStatus.Compensated, fixture.Saga.Status);
        Assert.Equal(HotelBookingPaymentStatus.Refunded, fixture.Saga.PaymentStatus);
        Assert.Equal(HotelRequestStatus.Failed, fixture.Request.Status);
        AssertTerminalConverged(fixture.Saga);
    }

    [Fact]
    public async Task Finalize_WhenPaidEventReplays_UsesCachedProviderBookingAndDoesNotCreateDuplicate()
    {
        var fixture = HotelSagaFixture.CreatePaid();
        var provider = new FakeHotelProvider();
        var mediator = new CapturingMediator(fixture.Request, "Paid");
        var handler = fixture.CreateHandler(provider, mediator);

        var command = new FinalizeHotelBookingAfterPaymentCommand(
            fixture.Request.OrderId!.Value,
            fixture.Request.UserId,
            Guid.NewGuid(),
            fixture.Saga.SagaId);

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, provider.CreateCount);
        Assert.Equal(1, provider.ConfirmCount);
        Assert.Null(mediator.CancelOrderCommand);
        Assert.Equal(HotelBookingSagaStatus.Completed, fixture.Saga.Status);
        Assert.Equal(HotelRequestStatus.Completed, fixture.Request.Status);
        Assert.Equal("PB-1", fixture.Request.ProviderBookingCode);
        AssertTerminalConverged(fixture.Saga);
    }

    [Fact]
    public async Task Finalize_WhenSameSuccessBatchReplaysRepeatedly_OutcomeDoesNotChange()
    {
        var fixture = HotelSagaFixture.CreatePaid();
        var provider = new FakeHotelProvider();
        var mediator = new CapturingMediator(fixture.Request, "Paid");
        var handler = fixture.CreateHandler(provider, mediator);

        var command = new FinalizeHotelBookingAfterPaymentCommand(
            fixture.Request.OrderId!.Value,
            fixture.Request.UserId,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            fixture.Saga.SagaId);

        for (var i = 0; i < 5; i++)
            await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, provider.CreateCount);
        Assert.Equal(1, provider.ConfirmCount);
        Assert.Null(mediator.CancelOrderCommand);
        Assert.Equal(HotelBookingSagaStatus.Completed, fixture.Saga.Status);
        Assert.Equal(HotelRequestStatus.Completed, fixture.Request.Status);
        Assert.Equal("PB-1", fixture.Request.ProviderBookingCode);
        AssertTerminalConverged(fixture.Saga);
    }

    [Fact]
    public async Task Finalize_WhenSameFailureBatchReplaysRepeatedly_OutcomeStaysCompensated()
    {
        var fixture = HotelSagaFixture.CreatePaid();
        var provider = new FakeHotelProvider { CreateFailure = new InvalidOperationException("provider down") };
        var mediator = new CapturingMediator(fixture.Request, "Paid");
        var handler = fixture.CreateHandler(provider, mediator);

        var command = new FinalizeHotelBookingAfterPaymentCommand(
            fixture.Request.OrderId!.Value,
            fixture.Request.UserId,
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            fixture.Saga.SagaId);

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, provider.CreateCount);
        Assert.NotNull(mediator.CancelOrderCommand);
        Assert.Equal(HotelBookingSagaStatus.Compensated, fixture.Saga.Status);
        Assert.Equal(HotelBookingPaymentStatus.Refunded, fixture.Saga.PaymentStatus);
        Assert.Equal(HotelRequestStatus.Failed, fixture.Request.Status);
        AssertTerminalConverged(fixture.Saga);
    }

    [Fact]
    public async Task Finalize_WhenProviderConfirmFailsAfterCode_RecoveryCompletesWithoutDuplicateCreate()
    {
        var fixture = HotelSagaFixture.CreatePaid();
        var provider = new FakeHotelProvider { ConfirmFailuresRemaining = 1 };
        var mediator = new CapturingMediator(fixture.Request, "Paid");
        var handler = fixture.CreateHandler(provider, mediator);

        var command = new FinalizeHotelBookingAfterPaymentCommand(
            fixture.Request.OrderId!.Value,
            fixture.Request.UserId,
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            fixture.Saga.SagaId);

        await Assert.ThrowsAsync<TimeoutException>(() => handler.Handle(command, CancellationToken.None));
        await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, provider.CreateCount);
        Assert.Equal(2, provider.ConfirmCount);
        Assert.Null(mediator.CancelOrderCommand);
        Assert.Equal(HotelBookingSagaStatus.Completed, fixture.Saga.Status);
        Assert.Equal(HotelRequestStatus.Completed, fixture.Request.Status);
        Assert.Equal("PB-1", fixture.Request.ProviderBookingCode);
        AssertTerminalConverged(fixture.Saga);
    }

    [Fact]
    public async Task CancelProviderBooking_WhenProviderSupportsCancellation_CancelsExternalBookingIdempotently()
    {
        var fixture = HotelSagaFixture.CreateCompensatedWithProviderBooking();
        var provider = new FakeHotelProvider { CancelStatus = "Cancelled" };
        var handler = fixture.CreateCancelHandler(provider);

        var command = new CancelProviderBookingCommand(
            fixture.Saga.SagaId,
            "payment refunded after provider booking",
            "cancel-idem-1");

        var first = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, provider.CancelCount);
        Assert.Equal("Cancelled", first.Outcome);
        Assert.Equal("Cancelled", replay.Outcome);
        Assert.False(first.ExternalUnresolved);
        Assert.Equal(HotelProviderBookingStatus.Cancelled, fixture.Saga.ProviderBookingStatus);
        Assert.Equal("cancel-idem-1", fixture.Saga.ProviderCancellationIdempotencyKey);
        AssertTerminalConverged(fixture.Saga);
    }

    [Fact]
    public async Task CancelProviderBooking_WhenProviderDoesNotSupportCancellation_MarksExternalStateUnresolved()
    {
        var fixture = HotelSagaFixture.CreateCompensatedWithProviderBooking();
        var provider = new FakeHotelProvider { CancelStatus = "Unsupported" };
        var handler = fixture.CreateCancelHandler(provider);

        var result = await handler.Handle(new CancelProviderBookingCommand(
            fixture.Saga.SagaId,
            "provider booking exists after compensation"),
            CancellationToken.None);

        Assert.Equal(1, provider.CancelCount);
        Assert.Equal("ExternallyUnresolved", result.Outcome);
        Assert.True(result.ExternalUnresolved);
        Assert.Equal(HotelProviderBookingStatus.ExternallyUnresolved, fixture.Saga.ProviderBookingStatus);
        Assert.NotNull(fixture.Saga.ExternalUnresolvedAt);
        AssertTerminalConverged(fixture.Saga);
    }

    private static void AssertTerminalConverged(HotelBookingSagaState saga)
        => Assert.True(
            saga.Status is HotelBookingSagaStatus.Completed or HotelBookingSagaStatus.Compensated,
            $"Saga must converge to Completed or Compensated, but was {saga.Status}.");

    private sealed class HotelSagaFixture
    {
        public HotelRequest Request { get; }
        public HotelBookingSagaState Saga { get; }
        private InMemoryHotelRequestRepository RequestRepository { get; }
        private InMemoryHotelBookingSagaRepository SagaRepository { get; }
        private InMemoryProviderBookingCacheRepository CacheRepository { get; }

        private HotelSagaFixture(HotelRequest request, HotelBookingSagaState saga)
        {
            Request = request;
            Saga = saga;
            RequestRepository = new InMemoryHotelRequestRepository(request);
            SagaRepository = new InMemoryHotelBookingSagaRepository(saga);
            CacheRepository = new InMemoryProviderBookingCacheRepository();
        }

        public static HotelSagaFixture CreatePaid()
        {
            var now = DateTime.UtcNow;
            var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var orderId = Guid.Parse("22222222-2222-2222-2222-222222222222");

            var request = HotelRequest.Create(
                userId,
                "SnappTrip",
                101,
                202,
                """{"checkIn":"2026-08-01","checkOut":"2026-08-03","rooms":1}""",
                """{"name":"Hotel"}""",
                """{"boardType":"BedBreakfast"}""",
                10_000_000,
                "IRR",
                "{}",
                null,
                """{"email":"guest@example.com","phone":"+989120000000","guests":[{"fullName":"Test User","age":30,"type":"Adult"}]}""",
                now,
                now.AddMinutes(30),
                "hotel-request-idem-1");

            request.ConvertToOrder(orderId, now.AddMinutes(1));

            var saga = HotelBookingSagaState.Start(userId, request.Id, now);
            saga.MarkOrderCreated(orderId, now.AddMinutes(1));
            saga.MarkPaymentPending(now.AddMinutes(2));
            saga.MarkPaid(orderId, now.AddMinutes(3));

            return new HotelSagaFixture(request, saga);
        }

        public static HotelSagaFixture CreateCompensatedWithProviderBooking()
        {
            var fixture = CreatePaid();
            var now = DateTime.UtcNow;

            fixture.Saga.MarkProviderBookingStarted(now);
            fixture.Saga.MarkProviderBookingConfirmed(now.AddSeconds(1));
            fixture.Request.MarkProviderConfirmed("PB-1", now.AddSeconds(1));
            fixture.Saga.Compensate("Payment was refunded after provider booking was created.", now.AddSeconds(2));

            return fixture;
        }

        public FinalizeHotelBookingAfterPaymentCommandHandler CreateHandler(
            IHotelProvider provider,
            IMediator mediator)
            => new(
                RequestRepository,
                SagaRepository,
                CacheRepository,
                provider,
                mediator,
                NullLogger<FinalizeHotelBookingAfterPaymentCommandHandler>.Instance);

        public CancelProviderBookingCommandHandler CreateCancelHandler(IHotelProvider provider)
            => new(
                RequestRepository,
                SagaRepository,
                CacheRepository,
                provider,
                NullLogger<CancelProviderBookingCommandHandler>.Instance);
    }

    private sealed class InMemoryHotelRequestRepository : IHotelRequestRepository
    {
        private readonly HotelRequest _request;

        public InMemoryHotelRequestRepository(HotelRequest request)
        {
            _request = request;
        }

        public Task<HotelRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_request.Id == id ? _request : null);

        public Task<HotelRequest?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_request.Id == id && _request.UserId == userId ? _request : null);

        public Task<HotelRequest?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_request.OrderId == orderId ? _request : null);

        public Task<HotelRequest?> GetByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_request.UserId == userId && _request.IdempotencyKey == idempotencyKey ? _request : null);

        public Task AddAsync(HotelRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemoryHotelBookingSagaRepository : IHotelBookingSagaRepository
    {
        private readonly HotelBookingSagaState _saga;

        public InMemoryHotelBookingSagaRepository(HotelBookingSagaState saga)
        {
            _saga = saga;
        }

        public Task<HotelBookingSagaState?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default)
            => Task.FromResult(_saga.SagaId == sagaId ? _saga : null);

        public Task<HotelBookingSagaState?> GetByHotelRequestIdAsync(Guid hotelRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult(_saga.HotelRequestId == hotelRequestId ? _saga : null);

        public Task<HotelBookingSagaState?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_saga.OrderId == orderId ? _saga : null);

        public Task<IReadOnlyList<HotelBookingSagaState>> GetStuckAsync(
            IReadOnlyCollection<HotelBookingSagaStatus> statuses,
            DateTime olderThanUtc,
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HotelBookingSagaState>>([_saga]);

        public Task AddAsync(HotelBookingSagaState saga, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemoryProviderBookingCacheRepository : IHotelProviderBookingCacheRepository
    {
        private readonly List<HotelProviderBookingCacheEntry> _entries = [];

        public Task<HotelProviderBookingCacheEntry?> GetAsync(
            string providerName,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.FirstOrDefault(e =>
                e.ProviderName == providerName && e.IdempotencyKey == idempotencyKey));

        public Task<HotelProviderBookingCacheEntry?> GetByProviderBookingCodeAsync(
            string providerName,
            string providerBookingCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.FirstOrDefault(e =>
                e.ProviderName == providerName && e.ProviderBookingCode == providerBookingCode));

        public Task<HotelProviderBookingCacheEntry?> GetBySagaIdAsync(
            Guid sagaId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.FirstOrDefault(e => e.SagaId == sagaId));

        public Task AddAsync(HotelProviderBookingCacheEntry entry, CancellationToken cancellationToken = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CapturingMediator : IMediator
    {
        private readonly HotelRequest _request;
        private readonly string _paymentState;

        public CapturingMediator(HotelRequest request, string paymentState)
        {
            _request = request;
            _paymentState = paymentState;
        }

        public CancelOrderCommand? CancelOrderCommand { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object? response = request switch
            {
                GetOrderByIdQuery => BuildOrder(),
                CancelOrderCommand command => CaptureCancel(command),
                _ => throw new NotSupportedException(request.GetType().FullName)
            };

            return Task.FromResult((TResponse)response!);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;

        private OrderDto BuildOrder()
            => new(
                _request.OrderId!.Value,
                "ORD-HOTEL-1",
                _request.UserId,
                _request.TotalPrice,
                0,
                0,
                null,
                0,
                _request.TotalPrice,
                "Processing",
                _paymentState,
                "Hotel",
                _request.Id,
                "HotelRequest",
                null,
                null,
                null,
                0,
                [],
                DateTimeOffset.UtcNow);

        private CancelOrderResponse CaptureCancel(CancelOrderCommand command)
        {
            CancelOrderCommand = command;
            return new CancelOrderResponse(command.OrderId, "Cancelled", "Refunded");
        }
    }

    private sealed class FakeHotelProvider : IHotelProvider
    {
        public Exception? CreateFailure { get; set; }
        public int ConfirmFailuresRemaining { get; set; }
        public string CancelStatus { get; set; } = "Cancelled";
        public int CreateCount { get; private set; }
        public int ConfirmCount { get; private set; }
        public int CancelCount { get; private set; }

        public Task<BookingCreateResultDto> CreateBookingAsync(BookingDraftDto dto)
        {
            CreateCount++;
            if (CreateFailure is not null)
                throw CreateFailure;

            return Task.FromResult(new BookingCreateResultDto
            {
                BookingCode = "PB-1",
                Price = 10_000_000,
                Currency = "IRR"
            });
        }

        public Task ConfirmBookingAsync(string bookingCode)
        {
            ConfirmCount++;
            if (ConfirmFailuresRemaining > 0)
            {
                ConfirmFailuresRemaining--;
                throw new TimeoutException("provider confirmation timeout");
            }

            return Task.CompletedTask;
        }

        public Task<BookingStatusDto> GetBookingStatusAsync(string bookingCode)
            => Task.FromResult(new BookingStatusDto { Status = "Pending" });

        public Task<CancelProviderBookingResultDto> CancelBookingAsync(
            string bookingCode,
            string idempotencyKey,
            string reason)
        {
            CancelCount++;
            return Task.FromResult(new CancelProviderBookingResultDto
            {
                Status = CancelStatus,
                ProviderMessage = CancelStatus == "Unsupported"
                    ? "provider cancellation endpoint not available"
                    : "cancelled"
            });
        }

        public Task<IEnumerable<GetCitiesResponse>> GetAllCities(string? name) => throw new NotSupportedException();
        public Task<GetAvailabilityByCityDto> GetAvailabilityByCity(GetAvailabilityByCityQuery query) => throw new NotSupportedException();
        public Task<IEnumerable<HotelDetailsDto>> GetHotelDetailsAsync(GetHotelDetailsQuery query) => throw new NotSupportedException();
        public Task<AvailabilityCalendarDto> GetHotelAvailabilityCalendarAsync(long hotelId, DateOnly from, DateOnly to) => throw new NotSupportedException();
        public Task<HotelReviewsDto> GetHotelReviewsAsync(long hotelId, int page = 1, int pageSize = 10) => throw new NotSupportedException();
        public Task<AccountBalanceDto> GetAccountBalanceAsync() => throw new NotSupportedException();
        public Task LockBookingAsync(string bookingCode) => throw new NotSupportedException();
    }
}
