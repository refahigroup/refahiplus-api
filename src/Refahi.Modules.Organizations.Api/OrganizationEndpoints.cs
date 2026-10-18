using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Organizations.Application.Contracts;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Organizations.Api;

public sealed record OrganizationBody(string Code, string Title, bool IsActive = true);
public sealed record MembershipBody(Guid UserId, DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil);
public sealed class OrganizationEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        var g = routes.MapGroup("/admin").RequireAuthorization("AdminOnly").WithTags("Organizations.Admin");
        g.MapGet("/", async (ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new ListOrganizationsQuery(), ct)))).WithName("Organizations.List");
        g.MapPost("/", async (OrganizationBody body, HttpContext h, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new SaveOrganizationCommand(null, body.Code, body.Title, body.IsActive, Actor(h)), ct)))).WithName("Organizations.Create");
        g.MapPut("/{id:guid}", async (Guid id, OrganizationBody body, HttpContext h, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new SaveOrganizationCommand(id, body.Code, body.Title, body.IsActive, Actor(h)), ct)))).WithName("Organizations.Update");
        g.MapGet("/{id:guid}/members", async (Guid id, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new ListOrganizationMembersQuery(id), ct)))).WithName("Organizations.ListMembers");
        g.MapPost("/{id:guid}/members", async (Guid id, MembershipBody body, HttpContext h, ISender sender, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await sender.Send(new AddOrganizationMemberCommand(id, body.UserId, Actor(h), body.ValidFrom, body.ValidUntil), ct)))).WithName("Organizations.AddMember");
        g.MapDelete("/{id:guid}/members/{memberId:guid}", async (Guid id, Guid memberId, HttpContext h, ISender sender, CancellationToken ct) => { await sender.Send(new RevokeOrganizationMemberCommand(id, memberId, Actor(h)), ct); return Results.Ok(ApiResponseHelper.Success(true)); }).WithName("Organizations.RevokeMember");
    }
    private static Guid Actor(HttpContext h) => Guid.Parse(h.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? h.User.FindFirstValue("sub")!);
}
