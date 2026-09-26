using System.Runtime.CompilerServices;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Shared.Services.Notification;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase04OtpFlowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    private static readonly DateTimeOffset Start = new(2026, 9, 16, 13, 0, 0, TimeSpan.Zero);
    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Identity_receipt_drives_OTP_reference_is_protected_and_success_binds_customer()
    {
        await using var db = Create(); await db.Database.MigrateAsync();
        var clock = new MutableClock(Start);
        var security = Security();
        var options = CheckoutConfiguration();
        var token = await SeedPaymentAsync(db, security, options, clock, "otp-key-0001", "OTP-1");
        var repository = new CreditPaymentsRepository(db);
        var customerId = Guid.NewGuid();
        var mediator = new IdentityMediator(new AuthenticationIdentityDto(
            customerId, true, "09121234567", OtpReceiptType.Sms));
        var notification = new FakeNotification();
        var logger = new CapturingLogger<SendCheckoutOtpHandler>();
        var protector = new DataProtectionCheckoutOtpReferenceProtector(new EphemeralDataProtectionProvider());
        var send = new SendCheckoutOtpHandler(repository, security, protector, notification, mediator,
            options, clock, logger);

        var challenge = await send.Handle(new(token, "alice"), default);

        Assert.Equal("09121234567", notification.SentReceipt);
        Assert.NotEqual("alice", notification.SentReceipt);
        var storedChallenge = await db.CheckoutOtpChallenges.AsNoTracking().SingleAsync();
        Assert.NotNull(storedChallenge.ProtectedReferenceCode);
        Assert.DoesNotContain(notification.ReferenceCode, storedChallenge.ProtectedReferenceCode!);
        Assert.Null(typeof(CheckoutOtpChallenge).GetProperty("OtpCode"));

        var verifyLogger = new CapturingLogger<VerifyCheckoutOtpHandler>();
        var verify = new VerifyCheckoutOtpHandler(repository, security, protector, notification, mediator,
            options, clock, verifyLogger);
        var verified = await verify.Handle(new(token, challenge.ChallengeId, notification.ValidOtp, null), default);

        Assert.Equal(notification.ReferenceCode, notification.ValidatedReference);
        Assert.Equal(notification.ValidOtp, notification.ValidatedOtp);
        Assert.NotNull(verified.AuthorizationSecret);
        db.ChangeTracker.Clear();
        var session = await db.CheckoutSessions.SingleAsync();
        var payment = await db.GatewayPayments.SingleAsync();
        Assert.Equal(customerId, session.CustomerId);
        Assert.Equal(customerId, payment.CustomerId);
        Assert.Equal(CheckoutSessionStatus.Authenticated, session.Status);
        Assert.Equal(GatewayPaymentStatus.AwaitingCustomer, payment.Status);
        Assert.DoesNotContain(verified.AuthorizationSecret!, Convert.ToBase64String(session.AuthorizationSecretHash!));
        Assert.All(logger.Messages.Concat(verifyLogger.Messages), message =>
        {
            Assert.DoesNotContain(notification.ValidOtp, message);
            Assert.DoesNotContain(notification.ReferenceCode, message);
            Assert.DoesNotContain(token, message);
        });

        var retry = await verify.Handle(new(token, challenge.ChallengeId, notification.ValidOtp,
            verified.AuthorizationSecret), default);
        Assert.True(retry.AlreadyAuthenticated);
        Assert.Null(retry.AuthorizationSecret);
    }

    [Fact]
    public async Task Enumeration_resend_attempt_cross_checkout_and_disable_race_are_normalized()
    {
        await using var db = Create(); await db.Database.MigrateAsync();
        var clock = new MutableClock(Start);
        var security = Security();
        var options = CheckoutConfiguration(maxSends: 2, maxAttempts: 2);
        var tokenA = await SeedPaymentAsync(db, security, options, clock, "otp-key-0002", "OTP-2");
        var tokenB = await SeedPaymentAsync(db, security, options, clock, "otp-key-0003", "OTP-3");
        var repository = new CreditPaymentsRepository(db);
        var customerId = Guid.NewGuid();
        var mediator = new IdentityMediator(new AuthenticationIdentityDto(
            customerId, true, "09120000000", OtpReceiptType.Sms));
        var notification = new FakeNotification();
        var protector = new DataProtectionCheckoutOtpReferenceProtector(new EphemeralDataProtectionProvider());
        var send = new SendCheckoutOtpHandler(repository, security, protector, notification, mediator,
            options, clock, new CapturingLogger<SendCheckoutOtpHandler>());

        var known = await send.Handle(new(tokenA, "known-user"), default);
        mediator.Resolution = null;
        var synthetic = await send.Handle(new(tokenB, "unknown-user"), default);
        Assert.Equal(known.Message, synthetic.Message);
        Assert.Equal(known.ExpiresAt - Start, synthetic.ExpiresAt - Start);
        Assert.Equal(1, notification.SendCount);

        var cooldown = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            send.Handle(new(tokenB, "unknown-user"), default));
        Assert.Equal("otp_rate_limited", cooldown.Code);
        Assert.Equal(429, cooldown.StatusCode);
        clock.Advance(TimeSpan.FromSeconds(61));
        var replacement = await send.Handle(new(tokenB, "unknown-user"), default);
        var challengesB = await db.CheckoutOtpChallenges.AsNoTracking()
            .Where(x => x.CheckoutSessionId == (db.CheckoutSessions.Single(s => s.TokenHash == Hash(security, tokenB)).Id))
            .OrderBy(x => x.SendSequence).ToListAsync();
        Assert.Equal(CheckoutOtpChallengeStatus.Superseded, challengesB[0].Status);
        Assert.Equal(CheckoutOtpChallengeStatus.Synthetic, challengesB[1].Status);
        clock.Advance(TimeSpan.FromSeconds(61));
        var maximum = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            send.Handle(new(tokenB, "unknown-user"), default));
        Assert.Equal("otp_rate_limited", maximum.Code);

        var verify = new VerifyCheckoutOtpHandler(repository, security, protector, notification, mediator,
            options, clock, new CapturingLogger<VerifyCheckoutOtpHandler>());
        var cross = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            verify.Handle(new(tokenB, known.ChallengeId, "000000", null), default));
        Assert.Equal("otp_invalid_or_expired", cross.Code);
        var syntheticFailure = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            verify.Handle(new(tokenB, replacement.ChallengeId, "000000", null), default));
        Assert.Equal("otp_invalid_or_expired", syntheticFailure.Code);

        mediator.Resolution = new AuthenticationIdentityDto(customerId, true, "09120000000", OtpReceiptType.Sms);
        clock.Advance(TimeSpan.FromSeconds(61));
        var realB = await SeedPaymentAsync(db, security, options, clock, "otp-key-0004", "OTP-4");
        var raceChallenge = await send.Handle(new(realB, "known-user"), default);
        mediator.Eligible = false;
        var disabled = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            verify.Handle(new(realB, raceChallenge.ChallengeId, notification.ValidOtp, null), default));
        Assert.Equal("otp_invalid_or_expired", disabled.Code);
        db.ChangeTracker.Clear();
        var raceSession = await db.CheckoutSessions.SingleAsync(x => x.TokenHash == Hash(security, realB));
        Assert.Null(raceSession.CustomerId);
        Assert.Equal(CheckoutSessionStatus.Created, raceSession.Status);
    }

    [Fact]
    public async Task Lost_cookie_can_be_replaced_only_for_bound_customer_before_expiry()
    {
        await using var db = Create(); await db.Database.MigrateAsync();
        var clock = new MutableClock(Start);
        var security = Security();
        var options = CheckoutConfiguration();
        var token = await SeedPaymentAsync(db, security, options, clock, "otp-reauth-0001", "OTP-REAUTH");
        var repository = new CreditPaymentsRepository(db);
        var customerId = Guid.NewGuid();
        var mediator = new IdentityMediator(new AuthenticationIdentityDto(
            customerId, true, "09120000000", OtpReceiptType.Sms));
        var notification = new FakeNotification();
        var protector = new DataProtectionCheckoutOtpReferenceProtector(new EphemeralDataProtectionProvider());
        var send = new SendCheckoutOtpHandler(repository, security, protector, notification, mediator,
            options, clock, new CapturingLogger<SendCheckoutOtpHandler>());
        var verify = new VerifyCheckoutOtpHandler(repository, security, protector, notification, mediator,
            options, clock, new CapturingLogger<VerifyCheckoutOtpHandler>());

        var firstChallenge = await send.Handle(new(token, "customer"), default);
        var first = await verify.Handle(new(token, firstChallenge.ChallengeId, notification.ValidOtp, null), default);
        Assert.NotNull(first.AuthorizationSecret);
        clock.Advance(TimeSpan.FromSeconds(61));

        mediator.Resolution = new AuthenticationIdentityDto(Guid.NewGuid(), true, "09121111111", OtpReceiptType.Sms);
        var sentBefore = notification.SendCount;
        var wrongCustomer = await send.Handle(new(token, "other-customer"), default);
        Assert.Equal(sentBefore, notification.SendCount);
        Assert.Equal(CheckoutOtpChallengeStatus.Synthetic,
            (await db.CheckoutOtpChallenges.SingleAsync(x => x.Id == wrongCustomer.ChallengeId)).Status);
        var denied = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            verify.Handle(new(token, wrongCustomer.ChallengeId, notification.ValidOtp, null), default));
        Assert.Equal("otp_invalid_or_expired", denied.Code);
        clock.Advance(TimeSpan.FromSeconds(61));

        mediator.Resolution = new AuthenticationIdentityDto(customerId, true, "09120000000", OtpReceiptType.Sms);
        var replacementChallenge = await send.Handle(new(token, "customer"), default);
        var replacement = await verify.Handle(new(token, replacementChallenge.ChallengeId, notification.ValidOtp, null), default);
        Assert.NotNull(replacement.AuthorizationSecret);
        Assert.NotEqual(first.AuthorizationSecret, replacement.AuthorizationSecret);
        db.ChangeTracker.Clear();
        var session = await db.CheckoutSessions.SingleAsync();
        Assert.Equal(customerId, session.CustomerId);
        Assert.False(security.VerifyAuthorizationSecret(first.AuthorizationSecret!, session.AuthorizationSecretHash!));
        Assert.True(security.VerifyAuthorizationSecret(replacement.AuthorizationSecret!, session.AuthorizationSecretHash!));

        var duplicate = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            verify.Handle(new(token, replacementChallenge.ChallengeId, notification.ValidOtp, null), default));
        Assert.Equal("otp_invalid_or_expired", duplicate.Code);
        clock.Advance(TimeSpan.FromMinutes(16));
        var expired = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            send.Handle(new(token, "customer"), default));
        Assert.Equal("checkout_expired", expired.Code);
    }

    [Fact]
    public async Task Concurrent_verification_of_one_challenge_rotates_secret_once()
    {
        await using var db = Create(); await db.Database.MigrateAsync();
        var clock = new MutableClock(Start);
        var security = Security();
        var options = CheckoutConfiguration();
        var token = await SeedPaymentAsync(db, security, options, clock, "otp-race-0001", "OTP-RACE");
        var customerId = Guid.NewGuid();
        var mediator = new IdentityMediator(new AuthenticationIdentityDto(
            customerId, true, "09120000000", OtpReceiptType.Sms));
        var notification = new FakeNotification();
        var protector = new DataProtectionCheckoutOtpReferenceProtector(new EphemeralDataProtectionProvider());
        var send = new SendCheckoutOtpHandler(new CreditPaymentsRepository(db), security, protector, notification,
            mediator, options, clock, new CapturingLogger<SendCheckoutOtpHandler>());
        var challenge = await send.Handle(new(token, "customer"), default);
        var tokenHash = Hash(security, token);
        var firstSecret = security.CreateAuthorizationSecret();
        var secondSecret = security.CreateAuthorizationSecret();

        await using var firstDb = Create();
        await using var secondDb = Create();
        async Task<Exception?> Attempt(CreditPaymentsDbContext context, byte[] secretHash)
        {
            try
            {
                await new CreditPaymentsRepository(context).AuthenticateCheckoutAsync(tokenHash,
                    challenge.ChallengeId, customerId, secretHash, Start.AddMinutes(15), Start, default);
                return null;
            }
            catch (Exception exception) { return exception; }
        }

        var results = await Task.WhenAll(Attempt(firstDb, firstSecret.SecretHash),
            Attempt(secondDb, secondSecret.SecretHash));
        Assert.Single(results, result => result is null);
        Assert.Single(results, result => result is CheckoutStateException { Code: "otp_invalid_or_expired" });
        db.ChangeTracker.Clear();
        var session = await db.CheckoutSessions.SingleAsync();
        var winningSecret = results[0] is null ? firstSecret.Secret : secondSecret.Secret;
        var losingSecret = results[0] is null ? secondSecret.Secret : firstSecret.Secret;
        Assert.True(security.VerifyAuthorizationSecret(winningSecret, session.AuthorizationSecretHash!));
        Assert.False(security.VerifyAuthorizationSecret(losingSecret, session.AuthorizationSecretHash!));
    }

    [Fact]
    public async Task Checkout_inquiry_preserves_processing_and_success_after_original_expiry()
    {
        await using var db = Create(); await db.Database.MigrateAsync();
        var clock = new MutableClock(Start);
        var security = Security();
        var options = CheckoutConfiguration();
        var token = await SeedPaymentAsync(db, security, options, clock, "late-result-0001", "LATE-RESULT");
        var customerId = Guid.NewGuid();
        var session = await db.CheckoutSessions.SingleAsync();
        var payment = await db.GatewayPayments.SingleAsync();
        session.Authenticate(customerId, Enumerable.Repeat((byte)7, 32).ToArray(),
            session.ExpiresAt, Start);
        payment.BindCustomer(customerId, Start);
        payment.BindOrder(Guid.NewGuid(), Start);
        session.BeginProcessing(Start.AddMinutes(1));
        payment.BeginProcessing("policy", "v1", new string('a', 64), Start.AddMinutes(1));
        await db.SaveChangesAsync();
        var get = new GetCheckoutHandler(new CreditPaymentsRepository(db), security, clock);
        clock.Advance(TimeSpan.FromMinutes(16));

        var processing = await get.Handle(new GetCheckoutQuery(token), default);
        Assert.Equal("processing", processing?.PaymentStatus);
        payment.MarkSucceeded(Guid.NewGuid(), Start.AddMinutes(14), clock.GetUtcNow());
        session.Complete(clock.GetUtcNow());
        await db.SaveChangesAsync();

        var succeeded = await get.Handle(new GetCheckoutQuery(token), default);
        Assert.Equal("succeeded", succeeded?.PaymentStatus);
        Assert.Equal("https://merchant.example/callback", succeeded?.ReturnUrl);
    }

    [Fact]
    public async Task Checkout_inquiry_reports_expiry_before_and_after_expiration_worker()
    {
        await using var db = Create(); await db.Database.MigrateAsync();
        var clock = new MutableClock(Start);
        var security = Security();
        var options = CheckoutConfiguration();
        var token = await SeedPaymentAsync(db, security, options, clock, "expired-result-0001", "EXPIRED-RESULT");
        var get = new GetCheckoutHandler(new CreditPaymentsRepository(db), security, clock);
        clock.Advance(TimeSpan.FromMinutes(16));

        var beforeWorker = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            get.Handle(new GetCheckoutQuery(token), default));
        Assert.Equal("checkout_expired", beforeWorker.Code);
        Assert.Equal(410, beforeWorker.StatusCode);

        var session = await db.CheckoutSessions.SingleAsync();
        var payment = await db.GatewayPayments.SingleAsync();
        payment.Expire(clock.GetUtcNow());
        session.Expire(clock.GetUtcNow());
        await db.SaveChangesAsync();

        var afterWorker = await Assert.ThrowsAsync<CreditPaymentsApplicationException>(() =>
            get.Handle(new GetCheckoutQuery(token), default));
        Assert.Equal("checkout_expired", afterWorker.Code);
        Assert.Equal(410, afterWorker.StatusCode);
    }

    private CreditPaymentsDbContext Create() => new(
        new DbContextOptionsBuilder<CreditPaymentsDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);

    private static IOptions<CheckoutOptions> CheckoutConfiguration(int maxSends=5,int maxAttempts=5) =>
        Options.Create(new CheckoutOptions
        {
            PublicBaseUrl = "https://pay.example",
            TokenKeys = new() { [1] = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray()) },
            OtpResendCooldownSeconds = 60,
            MaximumOtpSends = maxSends,
            MaximumOtpVerifyAttempts = maxAttempts,
            SyntheticChallengeExpirySeconds = 300
        });

    private static HmacCheckoutSecurityService Security() =>
        new(CheckoutConfiguration());

    private static byte[] Hash(ICheckoutSecurityService security,string token)
    {
        Assert.True(security.TryHashPresentedToken(token,out var hash));
        return hash;
    }

    private static async Task<string> SeedPaymentAsync(
        CreditPaymentsDbContext db,
        ICheckoutSecurityService security,
        IOptions<CheckoutOptions> checkoutOptions,
        TimeProvider clock,
        string key,
        string orderId)
    {
        var merchant = MerchantTestFactory.Create(Guid.NewGuid(), "Merchant", clock.GetUtcNow()); merchant.Activate(clock.GetUtcNow());
        var terminal = merchant.AddTerminal("Terminal", "store", 100, 1000,
            ["https://merchant.example/callback"], clock.GetUtcNow());
        db.Merchants.Add(merchant); await db.SaveChangesAsync();
        var repository = new CreditPaymentsRepository(db);
        var handler = new CreateGatewayPaymentHandler(repository, repository, repository, security,
            Options.Create(new GatewayPaymentOptions()), checkoutOptions, clock);
        var created = await handler.Handle(new(merchant.Id, terminal.Id, Guid.NewGuid(), key,
            orderId, 100, "IRR", "description", "https://merchant.example/callback", null, null), default);
        return new Uri(created.CheckoutUrl).Segments[^1];
    }

    private sealed class FakeNotification : INotificationService
    {
        public string ReferenceCode { get; } = "provider-reference-secret";
        public string ValidOtp { get; } = "654321";
        public string? SentReceipt { get; private set; }
        public string? ValidatedReference { get; private set; }
        public string? ValidatedOtp { get; private set; }
        public int SendCount { get; private set; }
        public Task<SendOtpResult> SendOtp(string receipt,OtpReceiptType type,OtpType otpType,CancellationToken ct)
        { SentReceipt=receipt;SendCount++;return Task.FromResult(new SendOtpResult(ReferenceCode,Start.AddMinutes(5).UtcDateTime)); }
        public Task<OtpValidationResult> ValidateOtp(string referenceCode,string otp,OtpType otpType,CancellationToken ct)
        { ValidatedReference=referenceCode;ValidatedOtp=otp;return Task.FromResult(new OtpValidationResult(otp==ValidOtp)); }
        public Task SendSms(string[] numbers,string body,string? sender=null,CancellationToken ct=default)=>Task.CompletedTask;
    }

    private sealed class IdentityMediator(AuthenticationIdentityDto? resolution) : IMediator
    {
        public AuthenticationIdentityDto? Resolution { get; set; } = resolution;
        public bool Eligible { get; set; } = true;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request,CancellationToken ct=default)
        {
            object? response=request switch
            {
                ResolveAuthenticationIdentityQuery => Resolution,
                GetUserAuthenticationEligibilityQuery q => new UserAuthenticationEligibilityDto(q.UserId,Eligible),
                _ => throw new NotSupportedException(request.GetType().Name)
            };
            return Task.FromResult((TResponse)response!);
        }
        public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task Publish(object notification,CancellationToken ct=default)=>Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification,CancellationToken ct=default) where TNotification:INotification=>Task.CompletedTask;
        public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,[EnumeratorCancellation] CancellationToken ct=default){yield break;}
        public async IAsyncEnumerable<object?> CreateStream(object request,[EnumeratorCancellation] CancellationToken ct=default){yield break;}
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current=now;
        public override DateTimeOffset GetUtcNow()=>current;
        public void Advance(TimeSpan value)=>current+=value;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; }=[];
        public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
        public bool IsEnabled(LogLevel logLevel)=>true;
        public void Log<TState>(LogLevel level,EventId eventId,TState state,Exception? exception,
            Func<TState,Exception?,string> formatter)=>Messages.Add(formatter(state,exception));
    }
}
