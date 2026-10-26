using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Application.Features.Vendor;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Notification;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class InPersonOtpRecoveryTests
{
    [Fact]
    public async Task UnknownDelivery_RepeatedStartReturnsSameOrderWithoutSendingOtp()
    {
        var fixture = new Fixture();
        fixture.Order.BeginOtpDispatch();
        var startedAt = fixture.Order.OtpDispatchStartedAt;

        var first = await fixture.Handler.Handle(fixture.Command, default);
        var second = await fixture.Handler.Handle(fixture.Command, default);

        Assert.Equal(fixture.Order.OrderId, first.OrderId);
        Assert.Equal(fixture.Order.Id, first.StoreOrderId);
        Assert.Equal(first, second);
        Assert.Null(first.OtpReferenceCode);
        Assert.Equal(startedAt, fixture.Order.OtpDispatchStartedAt);
        Assert.Equal(0, fixture.Notification.SendCount);
        Assert.Equal(0, fixture.Repository.UpdateCount);
    }

    [Fact]
    public async Task FailedInitialDispatch_CanRecoverThenExplicitlyResendForSameOrder()
    {
        var fixture = new Fixture();
        fixture.Notification.FailSend = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Handler.Handle(fixture.Command, default));
        Assert.NotNull(fixture.Order.OtpDispatchStartedAt);
        fixture.Notification.FailSend = false;

        var recovered = await fixture.Handler.Handle(fixture.Command, default);
        Assert.Null(recovered.OtpReferenceCode);
        Assert.Equal(1, fixture.Notification.SendCount);

        var resend = new ResendInPersonOrderOtpHandler(fixture.Notification, new Protector(), fixture.Repository, fixture.Mediator);
        var resent = await resend.Handle(new(fixture.Command.VendorUserId, recovered.OrderId), default);
        var retried = await fixture.Handler.Handle(fixture.Command, default);

        Assert.Equal(recovered.OrderId, resent.OrderId);
        Assert.Equal(recovered.StoreOrderId, resent.StoreOrderId);
        Assert.Equal("protected-reference", resent.OtpReferenceCode);
        Assert.Equal(resent, retried);
        Assert.Equal(2, fixture.Notification.SendCount);
    }

    [Fact]
    public async Task Recovery_RejectsChangedPayload()
    {
        var fixture = new Fixture();
        fixture.Order.BeginOtpDispatch();
        var ex = await Assert.ThrowsAsync<StoreDomainException>(() => fixture.Handler.Handle(
            fixture.Command with { AmountMinor = 200_000 }, default));
        Assert.Equal("IDEMPOTENCY_PAYLOAD_MISMATCH", ex.ErrorCode);
        Assert.Equal(0, fixture.Notification.SendCount);
    }

    [Fact]
    public async Task Recovery_RequiresCurrentVendorPermission()
    {
        var fixture = new Fixture();
        fixture.Order.BeginOtpDispatch();
        fixture.Mediator.Allowed = false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Handler.Handle(fixture.Command, default));
        Assert.Equal(0, fixture.Notification.SendCount);
    }

    private sealed class Fixture
    {
        public StartInPersonOrderCommand Command { get; }
        public StoreOrder Order { get; }
        public Repository Repository { get; }
        public RecoveryMediator Mediator { get; }
        public Notification Notification { get; } = new();
        public StartInPersonOrderHandler Handler { get; }

        public Fixture()
        {
            var buyer = Guid.NewGuid();
            var actor = Guid.NewGuid();
            var shop = Guid.NewGuid();
            var product = Guid.NewGuid();
            var supplier = Guid.NewGuid();
            Command = new(actor, shop, product, "09121234567", 100_000, "retry-key");
            var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"in-person-v1|{buyer:N}|{actor:N}|Vendor|{shop:N}|{product:N}|100000")));
            Order = StoreOrder.CreateInPerson(buyer, actor, "Vendor", shop, supplier,
                $"vendor:{actor:N}:retry-key", fingerprint,
                new StoreOrderItemSnapshot(Guid.Empty, product, null, null, null, "محصول حضوری",
                    null, null, 10, "store.service", supplier, shop, SalesChannel.InPerson,
                    ProductType.Service, SalesModel.Unlimited, FulfillmentMethod.Pickup,
                    1, 100_000, 0, 100_000, Guid.NewGuid(), Guid.NewGuid(), 10m, null, 0, 100_000));
            Order.AttachOrder(Guid.NewGuid());
            Repository = new(Order);
            Mediator = new(Order);
            Handler = new(null!, null!, Repository, null!, Notification, new Protector(), Mediator,
                TimeProvider.System, NullLogger<StartInPersonOrderHandler>.Instance);
        }
    }

    private sealed class Repository(StoreOrder order) : IStoreOrderRepository
    {
        public int UpdateCount { get; private set; }
        public Task<StoreOrder?> GetByIdempotencyKeyAsync(Guid userId, string key, CancellationToken ct = default)
        {
            Assert.Equal(order.UserId, userId);
            Assert.Equal(order.IdempotencyKey, key);
            return Task.FromResult<StoreOrder?>(order);
        }
        public Task<StoreOrder?> GetByOrderIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(id == order.OrderId ? order : null);
        public Task AddAsync(StoreOrder value, CancellationToken ct = default) => throw new InvalidOperationException("Must reuse existing order");
        public Task UpdateAsync(StoreOrder value, CancellationToken ct = default)
        {
            Assert.Same(order, value);
            UpdateCount++;
            return Task.CompletedTask;
        }
        public Task CommitPaidAsync(Guid id, CancellationToken ct = default) => throw new InvalidOperationException("Must not pay during recovery");
    }

    private sealed class RecoveryMediator(StoreOrder order) : IMediator
    {
        public bool Allowed { get; set; } = true;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            object value = request switch
            {
                GetOrderUserSummariesQuery => new[] { new OrderUserSummaryDto(order.UserId, null, null, "09121234567") },
                AuthorizeStoreResourceQuery => Allowed,
                GetOrderByIdQuery => new OrderDto(order.OrderId!.Value, "POS-1", order.UserId,
                    100_000, 0, 0, null, 0, 100_000, "Pending", "Unpaid", "Store", order.Id,
                    "StoreOrder", null, null, null, 0, [], DateTimeOffset.UnixEpoch),
                _ => throw new InvalidOperationException($"Unexpected request: {request.GetType().Name}")
            };
            return Task.FromResult((TResponse)value);
        }
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Notification : INotificationService
    {
        public bool FailSend { get; set; }
        public int SendCount { get; private set; }
        public Task<SendOtpResult> SendOtp(string receipt, OtpReceiptType type, OtpType otpType, CancellationToken ct)
        {
            SendCount++;
            Assert.Equal(OtpType.VendorInPersonPayment, otpType);
            if (FailSend) throw new HttpRequestException("Simulated send failure");
            return Task.FromResult(new SendOtpResult("provider-reference", DateTime.UtcNow.AddMinutes(5)));
        }
        public Task<OtpValidationResult> ValidateOtp(string referenceCode, string otp, OtpType otpType, CancellationToken ct) => throw new NotSupportedException();
        public Task SendSms(string[] phoneNumbers, string body, string? sender = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Protector : IInPersonOtpReferenceProtector
    {
        public string Protect(InPersonOtpReference reference) => "protected-reference";
        public bool TryUnprotect(string value, out InPersonOtpReference? reference) => throw new NotSupportedException();
    }
}
