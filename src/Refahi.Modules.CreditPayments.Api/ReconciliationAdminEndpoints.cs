using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.CreditPayments.Api;

public sealed record StartReconciliationRunRequest(string Kind, DateTimeOffset? From, DateTimeOffset? To, bool DryRun = false);
public sealed record AcknowledgeReconciliationFindingRequest(string? Note);

public sealed class ReconciliationAdminEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder r) return;
        r.MapPost("/admin/reconciliation/runs", Start).WithName("CreditPayments.Admin.Reconciliation.Start")
            .WithTags("CreditPayments.Admin.Reconciliation").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/reconciliation/runs", Runs).WithName("CreditPayments.Admin.Reconciliation.Runs")
            .WithTags("CreditPayments.Admin.Reconciliation").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/reconciliation/runs/{runId:guid}", Run).WithName("CreditPayments.Admin.Reconciliation.Run")
            .WithTags("CreditPayments.Admin.Reconciliation").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/reconciliation/findings", Findings).WithName("CreditPayments.Admin.Reconciliation.Findings")
            .WithTags("CreditPayments.Admin.Reconciliation").RequireAuthorization("AdminOnly");
        r.MapGet("/admin/reconciliation/findings/{findingId:guid}", Finding).WithName("CreditPayments.Admin.Reconciliation.Finding")
            .WithTags("CreditPayments.Admin.Reconciliation").RequireAuthorization("AdminOnly");
        r.MapPost("/admin/reconciliation/findings/{findingId:guid}/acknowledge", Acknowledge)
            .WithName("CreditPayments.Admin.Reconciliation.Acknowledge")
            .WithTags("CreditPayments.Admin.Reconciliation").RequireAuthorization("AdminOnly");
    }

    private static async Task<IResult> Start(StartReconciliationRunRequest request, ClaimsPrincipal user, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new StartReconciliationRunCommand(request.Kind, request.From, request.To,
            request.DryRun, $"admin-{Actor(user)}"), ct);
        return result is null
            ? Results.Conflict(ApiResponseHelper.Error("یک اجرای فعال از این نوع وجود دارد", statusCode: 409))
            : Results.Accepted(value: ApiResponseHelper.Success(result, "اجرای reconciliation تکمیل شد", 202));
    }
    private static async Task<IResult> Runs(string? kind, string? status, DateTimeOffset? from, DateTimeOffset? to,
        int? page, int? pageSize, IMediator mediator, CancellationToken ct) =>
        Paged(await mediator.Send(new SearchReconciliationRunsQuery(kind, status, from, to, page ?? 1, pageSize ?? 50), ct));
    private static async Task<IResult> Run(Guid runId, IMediator mediator, CancellationToken ct) =>
        await mediator.Send(new GetReconciliationRunQuery(runId), ct) is { } x
            ? Results.Ok(ApiResponseHelper.Success(x))
            : Results.NotFound(ApiResponseHelper.Error("اجرای reconciliation یافت نشد", statusCode: 404));
    private static async Task<IResult> Findings(string? code, string? severity, string? status, string? resourceType,
        Guid? resourceId, Guid? merchantId, Guid? terminalId, DateTimeOffset? from, DateTimeOffset? to,
        int? page, int? pageSize, IMediator mediator, CancellationToken ct) =>
        Paged(await mediator.Send(new SearchReconciliationFindingsQuery(code, severity, status, resourceType,
            resourceId, merchantId, terminalId, from, to, page ?? 1, pageSize ?? 50), ct));
    private static async Task<IResult> Finding(Guid findingId, IMediator mediator, CancellationToken ct) =>
        await mediator.Send(new GetReconciliationFindingQuery(findingId), ct) is { } x
            ? Results.Ok(ApiResponseHelper.Success(x))
            : Results.NotFound(ApiResponseHelper.Error("یافته reconciliation یافت نشد", statusCode: 404));
    private static async Task<IResult> Acknowledge(Guid findingId, AcknowledgeReconciliationFindingRequest request,
        ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        await mediator.Send(new AcknowledgeReconciliationFindingCommand(findingId, Actor(user), request.Note), ct)
            ? Results.Ok(ApiResponseHelper.Success(new { findingId }, "یافته تأیید مشاهده شد"))
            : Results.NotFound(ApiResponseHelper.Error("یافته باز reconciliation یافت نشد", statusCode: 404));
    private static string Actor(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? "admin";
    private static IResult Paged<T>(PagedAdminResult<T> x) => Results.Ok(ApiResponseHelper.SuccessPaginated(x.Items, x.Page, x.PageSize, (int)Math.Min(int.MaxValue, x.TotalCount)));
}
