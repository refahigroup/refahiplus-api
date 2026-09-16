using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Api.Endpoints;
using Refahi.Modules.PaymentGateway.Application.Contracts.Configuration;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetPaymentSession;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Application.Features.GetPaymentSession;
using Refahi.Modules.PaymentGateway.Application.Features.InitiatePayment;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Xunit;

namespace Refahi.Modules.PaymentGateway.Tests;

public sealed class PaymentGatewayApiSecurityTests
{
    [Fact]
    public void AuthenticatedEndpointsRequireUserOrAdmin_CallbacksRemainAnonymous()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IMediator, NoopMediator>();
        var app = builder.Build();
        var group = app.MapGroup("/api/payment-gateway");
        new InitiatePaymentEndpoint().Map(group);
        new GetPaymentSessionEndpoint().Map(group);
        new GetMyPaymentSessionsEndpoint().Map(group);
        new PaymentCallbackEndpoint().Map(group);
        new JibitCallbackEndpoint().Map(group);

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();
        var initiate = endpoints.Single(x => x.RoutePattern.RawText!.EndsWith("/initiate"));
        var session = endpoints.Single(x => x.RoutePattern.RawText!.Contains("/sessions/"));
        var mine = endpoints.Single(x => x.RoutePattern.RawText!.EndsWith("/my-sessions"));
        var callbacks = endpoints.Where(x => x.RoutePattern.RawText!.Contains("/callback/")).ToList();

        Assert.All([initiate, session, mine], endpoint =>
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == "UserOrAdmin"));
        Assert.All(callbacks, endpoint =>
            Assert.Empty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()));
        Assert.All(callbacks, endpoint =>
        {
            Assert.Contains(endpoint.Metadata,
                item => item.GetType().Name == "EnableRateLimitingAttribute");
            Assert.Contains(endpoint.Metadata,
                item => item.GetType().Name == "RequestSizeLimitAttribute");
        });
    }

    [Fact]
    public void ValidatorRejectsUnsafeRedirectAndMissingIdempotencyKey()
    {
        var validator = new InitiatePaymentCommandValidator(Options.Create(
            new FundingSafetyOptions
            {
                AllowedReturnHosts = ["app.refahi.test"],
                AllowRelativeReturnUrls = true,
            }));
        var command = new InitiatePaymentCommand(
            Guid.NewGuid(), Guid.NewGuid(), 1000, "IRR", PaymentGatewayProviderType.Sep,
            "https://api.refahi.test/callback", "javascript:alert(1)");

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(command.ReturnBaseUrl));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(command.IdempotencyKey));
    }

    [Fact]
    public void PublicCallbackBase_RequiresConfigurationOutsideDevelopment()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentGatewayCallbackUrlBuilder.Build(PaymentGatewayProviderType.Sep,
                null, "https", "attacker.test", "https", "attacker.test",
                allowRequestFallback: false));
    }

    [Fact]
    public async Task SessionQuery_ReturnsDataOnlyToOwningUser()
    {
        var ownerId = Guid.NewGuid();
        var session = PaymentGatewaySession.Create(Guid.NewGuid(), ownerId, Guid.NewGuid(),
            1000, "IRR", PaymentGatewayProviderType.Sep, "/result");
        var handler = new GetPaymentSessionQueryHandler(new OwnedSessionRepository(session));

        Assert.Null(await handler.Handle(
            new GetPaymentSessionQuery(session.Id, Guid.NewGuid()), default));
        Assert.NotNull(await handler.Handle(
            new GetPaymentSessionQuery(session.Id, ownerId), default));
    }

    private sealed class OwnedSessionRepository(PaymentGatewaySession session)
        : IPaymentGatewaySessionRepository
    {
        public Task<PaymentGatewaySession?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<PaymentGatewaySession?>(id == session.Id ? session : null);
        public Task<PaymentGatewaySession?> GetByInitiationKeyAsync(Guid userId, string key,
            CancellationToken ct = default) => Task.FromResult<PaymentGatewaySession?>(null);
        public Task<IReadOnlyList<PaymentGatewaySession>> GetByUserAsync(Guid userId, int take,
            PaymentSessionStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PaymentGatewaySession>>([]);
        public Task<bool> TryAddAsync(PaymentGatewaySession value,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> TryClaimAsync(Guid id, IReadOnlyCollection<PaymentSessionStatus> statuses,
            DateTimeOffset now, DateTimeOffset lease, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetRecoveryCandidatesAsync(DateTimeOffset now, int size,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetReversalCandidatesAsync(DateTimeOffset now, int size,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> TryClaimReversalAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task UpdateAsync(PaymentGatewaySession value,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoopMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object?> Send(object request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
