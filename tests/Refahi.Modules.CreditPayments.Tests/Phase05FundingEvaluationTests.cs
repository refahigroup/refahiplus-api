using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Features.GetOrderFundingEligibility;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase05FundingEvaluationTests
{
    private const long PaymentAmount = 10_000_000;

    [Theory]
    [InlineData(10_000_000, true, 0)]
    [InlineData(15_000_000, true, 0)]
    [InlineData(7_000_000, false, 3_000_000)]
    [InlineData(0, false, 10_000_000)]
    public async Task Trusted_summary_drives_sufficient_and_insufficient_states(
        long eligibleAmount,
        bool sufficient,
        long deficit)
    {
        var fixture = CreateFixture(eligibleAmount, sufficient, deficit);

        var result = await fixture.Handler.Handle(
            new EvaluateCheckoutFundingCommand("valid-token", "valid-secret"),
            default);

        Assert.Equal(sufficient, result.Funding.IsSufficient);
        Assert.Equal(eligibleAmount, result.Funding.EligibleAmountMinor);
        Assert.Equal(deficit, result.Funding.DeficitAmountMinor);
        Assert.Equal(
            sufficient ? GatewayPaymentStatus.AwaitingCustomer : GatewayPaymentStatus.AwaitingFunding,
            fixture.Context.Payment.Status);
        Assert.Equal(
            sufficient ? CheckoutSessionStatus.Authenticated : CheckoutSessionStatus.AwaitingFunding,
            fixture.Context.Session.Status);
        Assert.Equal(fixture.Mediator.OrderId, fixture.Context.Payment.OrderId);
        Assert.DoesNotContain(
            fixture.Mediator.RequestTypes,
            type => type.Contains("PayOrder", StringComparison.Ordinal)
                || type.Contains("PaymentIntent", StringComparison.Ordinal)
                || type.Contains("WalletFunding", StringComparison.Ordinal));

        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("WalletId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OrderId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Allocation", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Re_evaluation_returns_funding_state_to_authenticated_without_payment_execution()
    {
        var fixture = CreateFixture(7_000_000, false, 3_000_000);
        await fixture.Handler.Handle(
            new EvaluateCheckoutFundingCommand("valid-token", "valid-secret"),
            default);
        fixture.Mediator.Eligibility = Eligibility(
            fixture.Mediator.OrderId,
            fixture.CustomerId,
            10_000_000,
            true,
            0);

        var result = await fixture.Handler.Handle(
            new EvaluateCheckoutFundingCommand("valid-token", "valid-secret"),
            default);

        Assert.True(result.Funding.IsSufficient);
        Assert.Equal(GatewayPaymentStatus.AwaitingCustomer, fixture.Context.Payment.Status);
        Assert.Equal(CheckoutSessionStatus.Authenticated, fixture.Context.Session.Status);
        Assert.Equal(1, fixture.Repository.FundingBecameSufficientCount);
    }

    [Fact]
    public async Task Missing_or_wrong_checkout_authorization_is_rejected_before_order_creation()
    {
        foreach (var secret in new string?[] { null, "wrong-secret" })
        {
            var fixture = CreateFixture(10_000_000, true, 0);

            var exception = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
                fixture.Handler.Handle(
                    new EvaluateCheckoutFundingCommand("valid-token", secret),
                    default));

            Assert.Equal("checkout_authentication_required", exception.Code);
            Assert.DoesNotContain(
                fixture.Mediator.RequestTypes,
                type => type.Contains(nameof(CreateOrGetExternalPaymentOrderCommand), StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Disabled_identity_is_rejected_before_order_creation_or_funding_query()
    {
        var fixture = CreateFixture(10_000_000, true, 0);
        fixture.Mediator.IdentityEligible = false;

        var exception = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            fixture.Handler.Handle(
                new EvaluateCheckoutFundingCommand("valid-token", "valid-secret"),
                default));

        Assert.Equal("checkout_authentication_required", exception.Code);
        Assert.Single(fixture.Mediator.RequestTypes);
        Assert.Contains(nameof(GetUserAuthenticationEligibilityQuery), fixture.Mediator.RequestTypes[0]);
    }

    [Fact]
    public async Task Expired_payment_is_rejected_before_order_creation_or_funding_query()
    {
        var fixture = CreateFixture(
            10_000_000,
            true,
            0,
            new FixedTimeProvider(DateTimeOffset.UtcNow.AddHours(1)));

        var exception = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            fixture.Handler.Handle(
                new EvaluateCheckoutFundingCommand("valid-token", "valid-secret"),
                default));

        Assert.Equal("payment_expired", exception.Code);
        Assert.Empty(fixture.Mediator.RequestTypes);
        Assert.Null(fixture.Context.Payment.OrderId);
    }

    [Fact]
    public async Task Expired_checkout_authorization_does_not_expose_funding_information()
    {
        var now = DateTimeOffset.UtcNow;
        var fixture = CreateFixture(
            10_000_000,
            true,
            0,
            new FixedTimeProvider(now.AddMinutes(10)),
            TimeSpan.FromMinutes(5));

        var exception = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            fixture.Handler.Handle(
                new EvaluateCheckoutFundingCommand("valid-token", "valid-secret"),
                default));

        Assert.Equal("checkout_authentication_required", exception.Code);
        Assert.Empty(fixture.Mediator.RequestTypes);
    }

    [Fact]
    public async Task Inconsistent_trusted_result_fails_safe_and_is_audited()
    {
        var fixture = CreateFixture(7_000_000, false, 2_000_000);

        var exception = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            fixture.Handler.Handle(
                new EvaluateCheckoutFundingCommand("valid-token", "valid-secret"),
                default));

        Assert.Equal("payment_context_invalid", exception.Code);
        Assert.Equal(1, fixture.Repository.IntegrityFailureCount);
        Assert.Equal(GatewayPaymentStatus.AwaitingCustomer, fixture.Context.Payment.Status);
    }

    [Fact]
    public async Task Existing_order_binding_cannot_be_replaced_by_another_resolved_order()
    {
        var fixture = CreateFixture(10_000_000, true, 0);
        var existingOrderId = Guid.NewGuid();
        fixture.Context.Payment.BindOrder(existingOrderId, DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            fixture.Handler.Handle(
                new EvaluateCheckoutFundingCommand("valid-token", "valid-secret"),
                default));

        Assert.Equal("payment_context_invalid", exception.Code);
        Assert.Equal(existingOrderId, fixture.Context.Payment.OrderId);
        Assert.Equal(1, fixture.Repository.IntegrityFailureCount);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("source")]
    [InlineData("category")]
    public async Task Inconsistent_existing_order_identity_is_never_bound(string mismatch)
    {
        var fixture = CreateFixture(10_000_000, true, 0);
        fixture.Mediator.OrderMutation = order => mismatch switch
        {
            "user" => order with { UserId = Guid.NewGuid() },
            "amount" => order with { AmountMinor = order.AmountMinor + 1 },
            "currency" => order with { Currency = "USD" },
            "source" => order with { SourceReferenceId = Guid.NewGuid() },
            "category" => order with { CategoryCode = "hotel" },
            _ => order
        };

        var exception = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            fixture.Handler.Handle(
                new EvaluateCheckoutFundingCommand("valid-token", "valid-secret"),
                default));

        Assert.Equal("payment_context_invalid", exception.Code);
        Assert.Null(fixture.Context.Payment.OrderId);
        Assert.Equal(1, fixture.Repository.IntegrityFailureCount);
    }

    [Fact]
    public async Task Orders_commit_then_cpg_bind_failure_reuses_the_same_order_on_retry()
    {
        var fixture = CreateFixture(10_000_000, true, 0);
        fixture.Repository.BindFailuresRemaining = 1;

        var first = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            fixture.Handler.Handle(
                new EvaluateCheckoutFundingCommand("valid-token", "valid-secret"),
                default));
        var result = await fixture.Handler.Handle(
            new EvaluateCheckoutFundingCommand("valid-token", "valid-secret"),
            default);

        Assert.Equal("temporarily_unavailable", first.Code);
        Assert.Equal(fixture.Mediator.OrderId, fixture.Context.Payment.OrderId);
        Assert.Equal(fixture.Mediator.OrderId, fixture.Mediator.ResolvedOrderIds.Single());
        Assert.Equal(2, fixture.Repository.BindingAttempts);
        Assert.True(result.Funding.IsSufficient);
    }

    [Fact]
    public async Task Orders_funding_summary_is_used_instead_of_summing_raw_wallet_options()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var options = new OrderPaymentOptionsDto(
            orderId,
            "ORD-TEST",
            PaymentAmount,
            "IRR",
            "Unpaid",
            [
                new OrderWalletOptionDto(
                    Guid.NewGuid(), "ORG_CREDIT", 8_000_000, 8_000_000, 0,
                    "IRR", "store", DateTimeOffset.UtcNow.AddDays(1), true, null),
                new OrderWalletOptionDto(
                    Guid.NewGuid(), "REFAHI", 7_000_000, 7_000_000, 0,
                    "IRR", null, null, true, null)
            ],
            [],
            7_000_000,
            false,
            3_000_000);
        var handler = new GetOrderFundingEligibilityQueryHandler(
            new PaymentOptionsMediator(options),
            TimeProvider.System);

        var result = await handler.Handle(
            new GetOrderFundingEligibilityQuery(orderId, userId),
            default);

        Assert.NotNull(result);
        Assert.Equal(7_000_000, result.TotalEligibleSpendableAmountMinor);
        Assert.Equal(3_000_000, result.DeficitAmountMinor);
        Assert.False(result.IsSufficient);
        Assert.True(result.HasPermittedPersonalWallet);
    }

    [Fact]
    public void Order_binding_is_immutable_and_funding_transitions_are_explicit()
    {
        var now = DateTimeOffset.UtcNow;
        var customerId = Guid.NewGuid();
        var payment = GatewayPayment.Create(
            Guid.NewGuid(), Guid.NewGuid(), "ORDER", PaymentAmount, "IRR", null, "{}",
            "https://merchant.example/callback", now.AddMinutes(15), "Merchant", "Terminal",
            "store", now);
        payment.ActivateCheckout(now);
        payment.BindCustomer(customerId, now);
        var orderId = Guid.NewGuid();

        payment.BindOrder(orderId, now);
        payment.BindOrder(orderId, now);
        payment.MarkAwaitingFunding(now);
        payment.MarkFundingSufficient(now);

        Assert.Equal(orderId, payment.OrderId);
        Assert.NotNull(payment.OrderBoundAt);
        Assert.Throws<CreditPaymentsDomainException>(() => payment.BindOrder(Guid.NewGuid(), now));
        Assert.Equal(GatewayPaymentStatus.AwaitingCustomer, payment.Status);
    }

    private static Fixture CreateFixture(
        long eligible,
        bool sufficient,
        long deficit,
        TimeProvider? clock = null,
        TimeSpan? authorizationLifetime = null)
    {
        var now = DateTimeOffset.UtcNow;
        var customerId = Guid.NewGuid();
        var merchant = Merchant.Create(Guid.NewGuid(), "Merchant", now);
        merchant.Activate(now);
        var payment = GatewayPayment.Create(
            merchant.Id, Guid.NewGuid(), "M-ORDER", PaymentAmount, "IRR", null, "{}",
            "https://merchant.example/callback", now.AddMinutes(15), "Merchant", "Terminal",
            "store", now);
        payment.ActivateCheckout(now);
        payment.BindCustomer(customerId, now);
        var session = CheckoutSession.Create(
            Guid.NewGuid(), payment.Id, new byte[32], Enumerable.Repeat((byte)7, 32).ToArray(),
            1, payment.ExpiresAt, now);
        session.Authenticate(
            customerId,
            Enumerable.Repeat((byte)9, 32).ToArray(),
            now.Add(authorizationLifetime ?? TimeSpan.FromMinutes(15)),
            now);
        var context = new FundingEvaluationContext(session, payment, merchant);
        var repository = new FakeCheckoutRepository(context);
        var mediator = new FundingMediator(customerId, payment, merchant, eligible, sufficient, deficit);
        var handler = new EvaluateCheckoutFundingHandler(
            repository,
            new FakeSecurity(),
            mediator,
            clock ?? TimeProvider.System,
            NullLogger<EvaluateCheckoutFundingHandler>.Instance);
        return new Fixture(handler, repository, mediator, context, customerId);
    }

    private static OrderFundingEligibilityDto Eligibility(
        Guid orderId,
        Guid userId,
        long eligible,
        bool sufficient,
        long deficit) => new(
            orderId,
            userId,
            PaymentAmount,
            "IRR",
            eligible,
            sufficient,
            deficit,
            true,
            DateTimeOffset.UtcNow);

    private sealed record Fixture(
        EvaluateCheckoutFundingHandler Handler,
        FakeCheckoutRepository Repository,
        FundingMediator Mediator,
        FundingEvaluationContext Context,
        Guid CustomerId);

    private sealed class FakeSecurity : ICheckoutSecurityService
    {
        public bool TryHashPresentedToken(string token, out byte[] tokenHash)
        {
            tokenHash = Enumerable.Repeat((byte)7, 32).ToArray();
            return token == "valid-token";
        }

        public bool VerifyAuthorizationSecret(string secret, byte[] expectedHash) =>
            secret == "valid-secret";

        public CheckoutTokenMaterial CreateCheckoutToken(Guid checkoutSessionId, Guid gatewayPaymentId) =>
            throw new NotSupportedException();
        public string RecreateCheckoutToken(CheckoutSession checkoutSession) =>
            throw new NotSupportedException();
        public CheckoutAuthorizationMaterial CreateAuthorizationSecret() =>
            throw new NotSupportedException();
    }

    private sealed class FundingMediator(
        Guid customerId,
        GatewayPayment payment,
        Merchant merchant,
        long eligible,
        bool sufficient,
        long deficit) : IMediator
    {
        public Guid OrderId { get; } = Guid.NewGuid();
        public bool IdentityEligible { get; set; } = true;
        public List<string> RequestTypes { get; } = [];
        public OrderFundingEligibilityDto Eligibility { get; set; } =
            Phase05FundingEvaluationTests.Eligibility(
                Guid.Empty,
                customerId,
                eligible,
                sufficient,
                deficit);
        public HashSet<Guid> ResolvedOrderIds { get; } = [];
        public Func<ExternalPaymentOrderDto, ExternalPaymentOrderDto>? OrderMutation { get; set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            RequestTypes.Add(request.GetType().Name);
            object response = request switch
            {
                GetUserAuthenticationEligibilityQuery =>
                    new UserAuthenticationEligibilityDto(customerId, IdentityEligible),
                CreateOrGetExternalPaymentOrderCommand => ResolveOrder(
                    new ExternalPaymentOrderDto(
                        OrderId,
                        customerId,
                        "CreditPayments",
                        payment.Id,
                        "CreditPayments",
                        merchant.SupplierId,
                        payment.AmountMinor,
                        "IRR",
                        payment.ApprovedCategoryCodeSnapshot,
                        "Unpaid",
                        payment.ExpiresAt,
                        true)),
                GetOrderFundingEligibilityQuery => Eligibility with { OrderId = OrderId },
                _ => throw new NotSupportedException(request.GetType().FullName)
            };
            return Task.FromResult((TResponse)response);
        }

        private ExternalPaymentOrderDto ResolveOrder(ExternalPaymentOrderDto order)
        {
            ResolvedOrderIds.Add(order.OrderId);
            return OrderMutation?.Invoke(order) ?? order;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeCheckoutRepository(FundingEvaluationContext context)
        : ICheckoutRepository
    {
        public int IntegrityFailureCount { get; private set; }
        public int FundingBecameSufficientCount { get; private set; }
        public int BindFailuresRemaining { get; set; }
        public int BindingAttempts { get; private set; }

        public Task<FundingEvaluationContext?> GetFundingEvaluationContextAsync(
            byte[] tokenHash,
            CancellationToken ct) => Task.FromResult<FundingEvaluationContext?>(context);

        public Task BindOrderAsync(
            Guid checkoutSessionId,
            Guid gatewayPaymentId,
            Guid customerId,
            Guid orderId,
            DateTimeOffset now,
            bool recovered,
            CancellationToken ct)
        {
            BindingAttempts++;
            if (BindFailuresRemaining > 0)
            {
                BindFailuresRemaining--;
                throw new IOException("Simulated CPG persistence failure after Orders commit");
            }
            context.Payment.BindOrder(orderId, now);
            return Task.CompletedTask;
        }

        public Task<FundingStatePersistenceResult> ApplyFundingEvaluationAsync(
            Guid checkoutSessionId,
            Guid gatewayPaymentId,
            Guid customerId,
            Guid orderId,
            bool isSufficient,
            long eligibleAmountMinor,
            long deficitAmountMinor,
            DateTimeOffset evaluatedAt,
            CancellationToken ct)
        {
            var wasAwaiting = context.Payment.Status == GatewayPaymentStatus.AwaitingFunding;
            if (isSufficient)
            {
                context.Payment.MarkFundingSufficient(evaluatedAt);
                context.Session.MarkFundingSufficient(evaluatedAt);
            }
            else
            {
                context.Payment.MarkAwaitingFunding(evaluatedAt);
                context.Session.MarkAwaitingFunding(evaluatedAt);
            }
            if (wasAwaiting && isSufficient)
                FundingBecameSufficientCount++;
            return Task.FromResult(new FundingStatePersistenceResult(
                context.Session,
                context.Payment,
                wasAwaiting && isSufficient));
        }

        public Task RecordFundingIntegrityFailureAsync(
            Guid gatewayPaymentId,
            Guid customerId,
            DateTimeOffset now,
            CancellationToken ct)
        {
            IntegrityFailureCount++;
            return Task.CompletedTask;
        }

        public Task<CheckoutLookup?> FindCheckoutAsync(byte[] tokenHash, bool tracking, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<CheckoutSession?> GetCheckoutForPaymentAsync(Guid paymentId, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<OtpChallengeDispatch> ReserveOtpChallengeAsync(byte[] tokenHash, Guid? resolvedCustomerId, short otpType, DateTimeOffset now, DateTimeOffset provisionalExpiresAt, TimeSpan resendCooldown, int maximumSends, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task FinalizeOtpChallengeAsync(Guid challengeId, string? protectedReferenceCode, DateTimeOffset expiresAt, bool synthetic, DateTimeOffset now, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task MarkOtpDispatchFailedAsync(Guid challengeId, DateTimeOffset now, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<OtpChallengeDispatch?> BeginOtpVerificationAsync(byte[] tokenHash, Guid challengeId, DateTimeOffset now, int maximumAttempts, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task RecordOtpValidationFailureAsync(Guid challengeId, int maximumAttempts, DateTimeOffset now, string auditOutcome, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task AuthenticateCheckoutAsync(byte[] tokenHash, Guid challengeId, Guid customerId, byte[] authorizationSecretHash, DateTimeOffset authorizationExpiresAt, DateTimeOffset now, CancellationToken ct) =>
            throw new NotSupportedException();
    }


    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PaymentOptionsMediator(OrderPaymentOptionsDto options) : IMediator
    {
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            if (request is not GetOrderPaymentOptionsQuery)
                throw new NotSupportedException(request.GetType().FullName);
            return Task.FromResult((TResponse)(object)options);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
