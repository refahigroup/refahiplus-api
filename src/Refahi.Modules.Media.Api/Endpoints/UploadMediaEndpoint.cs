using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Media.Application.Contracts.Commands;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Media.Api.Endpoints;

public class UploadMediaEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/upload", async (
            IFormFile file,
            HttpContext http,
            IMediator mediator,
            [FromQuery] string? entityType,
            [FromQuery] Guid? entityId,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest(ApiResponseHelper.Error("فایلی ارسال نشده است"));

            var userIdClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? http.User.FindFirstValue("sub");
            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            await using var stream = file.OpenReadStream();
            var command = new UploadMediaCommand(
                stream, file.FileName, file.ContentType, file.Length,
                userId, entityType, entityId);

            var result = await mediator.Send(command, ct);
            return Results.Created(result.Url,
                ApiResponseHelper.Success(result, "فایل با موفقیت آپلود شد", 201));
        })
        .WithName("Media.Upload")
        .WithTags("Media")
        .RequireAuthorization()
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<ApiResponse<UploadMediaResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithMetadata(new RequestSizeLimitAttribute(MediaConstants.MaxRequestBodyBytes));
    }
}
