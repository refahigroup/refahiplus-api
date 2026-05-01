using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Media.Application.Contracts.Commands;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Media.Api.Endpoints;

public class UploadMediaBatchEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/upload/batch", async (
            HttpContext http,
            IMediator mediator,
            [FromQuery] string? entityType,
            [FromQuery] Guid? entityId,
            CancellationToken ct) =>
        {
            var form = await http.Request.ReadFormAsync(ct);
            var files = form.Files;

            if (files.Count == 0)
                return Results.BadRequest(ApiResponseHelper.Error("هیچ فایلی ارسال نشده است"));

            if (files.Count > MediaConstants.MaxBatchFileCount)
                return Results.BadRequest(ApiResponseHelper.Error(
                    $"حداکثر {MediaConstants.MaxBatchFileCount} فایل در هر بَچ مجاز است"));

            var userIdClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? http.User.FindFirstValue("sub");
            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var results = new List<UploadMediaResponse>();
            foreach (var file in files)
            {
                if (file.Length == 0) continue;
                await using var stream = file.OpenReadStream();
                var command = new UploadMediaCommand(
                    stream, file.FileName, file.ContentType, file.Length,
                    userId, entityType, entityId);
                results.Add(await mediator.Send(command, ct));
            }

            return Results.Ok(ApiResponseHelper.Success(results, "فایل‌ها با موفقیت آپلود شدند"));
        })
        .WithName("Media.UploadBatch")
        .WithTags("Media")
        .RequireAuthorization()
        .DisableAntiforgery()
        .Accepts<IFormFileCollection>("multipart/form-data")
        .Produces<ApiResponse<List<UploadMediaResponse>>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithMetadata(new RequestSizeLimitAttribute(MediaConstants.MaxBatchBodyBytes));
    }
}
