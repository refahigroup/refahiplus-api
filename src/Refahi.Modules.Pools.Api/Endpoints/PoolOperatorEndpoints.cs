using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Shared.Presentation;
using static Refahi.Modules.Pools.Api.Endpoints.PoolEndpointResponse;
namespace Refahi.Modules.Pools.Api.Endpoints;
public sealed record VoucherInput(Guid PoolId, string Code);
public sealed record RedemptionInput(Guid PoolId, string Code, string IdempotencyKey);
public sealed record GrantInput(Guid UserId, Guid PoolId);
public sealed class PoolOperatorEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        Named(routes.MapGet("/operator/pools", (HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new GetOperatorPoolsQuery(User(h)), ct)); }), "Operator.Pools").RequireAuthorization();
        Named(routes.MapPost("/operator/vouchers/validate", (VoucherInput r, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new ValidatePoolVoucherQuery(User(h), r.PoolId, r.Code), ct)); }), "Operator.Validate").RequireAuthorization().RequireRateLimiting("VoucherRedeem");
        Named(routes.MapPost("/operator/vouchers/redeem", (RedemptionInput r, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new RedeemPoolVoucherCommand(User(h), r.PoolId, r.Code, (r.IdempotencyKey ?? "").Trim()), ct)); }), "Operator.Redeem").RequireAuthorization().RequireRateLimiting("VoucherRedeem");
        Named(routes.MapGet("/admin/operators", (HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new ListPoolOperatorsQuery(), ct)); }), "Admin.Operators").RequireAuthorization("AdminOnly");
        Named(routes.MapPost("/admin/operators", (GrantInput r, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new GrantPoolOperatorCommand(User(h), r.UserId, r.PoolId), ct)); }), "Admin.Grant").RequireAuthorization("AdminOnly");
        Named(routes.MapDelete("/admin/operators/{id:guid}", (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new RevokePoolOperatorCommand(User(h), id), ct)); }), "Admin.Revoke").RequireAuthorization("AdminOnly");
        Named(routes.MapPost("/admin/legacy/cleanup", (HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new CleanupLegacyPoolsCommand(User(h)), ct)); }), "Admin.LegacyCleanup").RequireAuthorization("AdminOnly");
    }
}
