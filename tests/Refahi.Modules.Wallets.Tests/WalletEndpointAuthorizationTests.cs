using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Wallets.Api;
using Refahi.Modules.Wallets.Api.Endpoints;
using Refahi.Modules.Wallets.Api.Endpoints.Admin;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;
using Refahi.Shared.Presentation;
using Xunit;

namespace Refahi.Modules.Wallets.Tests;

public sealed class WalletEndpointAuthorizationTests
{
    [Fact]
    public async Task OwnerCanAccess_ButAnotherUserAndAnonymousCannot()
    {
        var ownerId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var sender = new WalletInfoSender(new WalletInfoDto(walletId, ownerId, 1, 1, "IRR", null, null));

        Assert.True(await WalletEndpointAuthorization.CanAccessWalletAsync(
            Context(ownerId, "User"), sender, walletId, CancellationToken.None));
        Assert.False(await WalletEndpointAuthorization.CanAccessWalletAsync(
            Context(Guid.NewGuid(), "User"), sender, walletId, CancellationToken.None));
        Assert.False(await WalletEndpointAuthorization.CanAccessWalletAsync(
            new DefaultHttpContext(), sender, walletId, CancellationToken.None));
    }

    [Fact]
    public async Task AdminCanAccessWithoutOwningWallet()
    {
        var sender = new WalletInfoSender(null);

        Assert.True(await WalletEndpointAuthorization.CanAccessWalletAsync(
            Context(Guid.NewGuid(), "Admin"), sender, Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(0, sender.SendCount);
    }

    [Theory]
    [MemberData(nameof(PrivilegedEndpoints))]
    public void PrivilegedMutationAndMaintenanceEndpointsRequireAdminOnly(IEndpoint endpoint)
    {
        var builder = WebApplication.CreateBuilder();
        var sender = new WalletInfoSender(null);
        builder.Services.AddSingleton<IMediator>(sender);
        builder.Services.AddSingleton<ISender>(sender);
        var app = builder.Build();
        endpoint.Map(app);

        var route = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Single();
        var authorization = route.Metadata.GetOrderedMetadata<IAuthorizeData>();

        Assert.Contains(authorization, item => item.Policy == "AdminOnly");
    }

    public static TheoryData<IEndpoint> PrivilegedEndpoints => new()
    {
        new TopUpWalletEndpoint(),
        new DetectDriftEndpoint(),
        new RebuildBalanceEndpoint(),
        new RebuildBalancesBatchEndpoint(),
        new RepairOrphanPaymentIntentHoldEndpoint(),
    };

    private static HttpContext Context(Guid subject, string role)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, subject.ToString()), new Claim(ClaimTypes.Role, role)],
            "test"));
        return context;
    }

    private sealed class WalletInfoSender(WalletInfoDto? wallet) : IMediator
    {
        public int SendCount { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            SendCount++;
            return Task.FromResult((TResponse)(object?)wallet!);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken ct = default) => throw new NotSupportedException();
    }
}
