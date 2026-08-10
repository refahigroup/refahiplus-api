using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Vendor;

public sealed record CreateStoreAccessRequest(
    string MobileNumber,
    string ResourceType,
    Guid ResourceId,
    string Role
);

public sealed record UpdateStoreAccessRequest(string Role, bool IsActive);

public sealed class StoreAccessEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet(
                "/vendor/context",
                async (ClaimsPrincipal principal, IMediator mediator, CancellationToken ct) =>
                {
                    if (!TryActor(principal, out var userId))
                        return Results.Unauthorized();
                    var contexts = await mediator.Send(new GetStoreVendorContextsQuery(userId), ct);
                    return Results.Ok(ApiResponseHelper.Success(contexts));
                }
            )
            .WithName("Store.Vendor.Context")
            .WithTags("Store.Vendor.Access")
            .RequireAuthorization("VendorOnly");

        routes
            .MapGet(
                "/admin/vendors/{vendorId:guid}/access-assignments",
                async (Guid vendorId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await mediator.Send(new GetStoreAccessAssignmentsQuery(vendorId), ct)
                        )
                    )
            )
            .WithName("Store.Admin.VendorAccess.List")
            .WithTags("Store.Admin.VendorAccess")
            .RequireAuthorization("AdminOnly");

        routes
            .MapGet(
                "/admin/vendors/access-summaries",
                async (Guid[] vendorIds, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await mediator.Send(new GetStoreAccessSummariesQuery(vendorIds), ct)
                        )
                    )
            )
            .WithName("Store.Admin.VendorAccess.Summaries")
            .WithTags("Store.Admin.VendorAccess")
            .RequireAuthorization("AdminOnly");

        routes
            .MapPost(
                "/admin/vendors/{vendorId:guid}/access-assignments",
                async (
                    Guid vendorId,
                    CreateStoreAccessRequest body,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryActor(principal, out var actorId))
                        return Results.Unauthorized();
                    var users = await mediator.Send(
                        new GetOrderUserSummariesQuery(MobileNumber: body.MobileNumber),
                        ct
                    );
                    var normalized = NormalizeMobile(body.MobileNumber);
                    var user = users.SingleOrDefault(x =>
                        string.Equals(x.MobileNumber, normalized, StringComparison.Ordinal)
                    );
                    if (user is null)
                        return Results.NotFound(
                            ApiResponseHelper.Error(
                                "کاربر فعالی با این شماره موبایل یافت نشد",
                                statusCode: 404
                            )
                        );
                    var result = await mediator.Send(
                        new CreateStoreAccessAssignmentCommand(
                            vendorId,
                            user.UserId,
                            body.ResourceType,
                            body.ResourceId,
                            body.Role,
                            actorId
                        ),
                        ct
                    );
                    return Results.Created(
                        $"/api/store/admin/vendors/{vendorId}/access-assignments/{result.Id}",
                        ApiResponseHelper.Success(result, "دسترسی با موفقیت ایجاد شد", 201)
                    );
                }
            )
            .WithName("Store.Admin.VendorAccess.Create")
            .WithTags("Store.Admin.VendorAccess")
            .RequireAuthorization("AdminOnly");

        routes
            .MapPut(
                "/admin/vendors/{vendorId:guid}/access-assignments/{assignmentId:guid}",
                async (
                    Guid vendorId,
                    Guid assignmentId,
                    UpdateStoreAccessRequest body,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryActor(principal, out var actorId))
                        return Results.Unauthorized();
                    var result = await mediator.Send(
                        new UpdateStoreAccessAssignmentCommand(
                            vendorId,
                            assignmentId,
                            body.Role,
                            body.IsActive,
                            actorId
                        ),
                        ct
                    );
                    return result is null
                        ? Results.NotFound(
                            ApiResponseHelper.Error("دسترسی یافت نشد", statusCode: 404)
                        )
                        : Results.Ok(ApiResponseHelper.Success(result, "دسترسی بروزرسانی شد"));
                }
            )
            .WithName("Store.Admin.VendorAccess.Update")
            .WithTags("Store.Admin.VendorAccess")
            .RequireAuthorization("AdminOnly");

        routes
            .MapDelete(
                "/admin/vendors/{vendorId:guid}/access-assignments/{assignmentId:guid}",
                async (
                    Guid vendorId,
                    Guid assignmentId,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryActor(principal, out var actorId))
                        return Results.Unauthorized();
                    var found = await mediator.Send(
                        new RevokeStoreAccessAssignmentCommand(vendorId, assignmentId, actorId),
                        ct
                    );
                    return found
                        ? Results.Ok(
                            ApiResponseHelper.Success(new { assignmentId }, "دسترسی لغو شد")
                        )
                        : Results.NotFound(
                            ApiResponseHelper.Error("دسترسی یافت نشد", statusCode: 404)
                        );
                }
            )
            .WithName("Store.Admin.VendorAccess.Revoke")
            .WithTags("Store.Admin.VendorAccess")
            .RequireAuthorization("AdminOnly");
    }

    private static bool TryActor(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"),
            out actorId
        );

    private static string NormalizeMobile(string value)
    {
        if (
            !MobileNumberSearchNormalizer.TryNormalize(value, out var normalized)
            || normalized is null
        )
            return value;
        return normalized.StartsWith("98", StringComparison.Ordinal) && normalized.Length == 12
            ? $"0{normalized[2..]}"
            : normalized;
    }
}
