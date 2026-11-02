using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Media.Infrastructure.Services;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Media.Api.Endpoints;

public sealed record PurgeExternalMediaCacheRequest(string? Module, string? Provider);
public sealed class PurgeExternalMediaCacheValidator : AbstractValidator<PurgeExternalMediaCacheRequest>
{
    public PurgeExternalMediaCacheValidator()
    {
        RuleFor(x => x.Module).MaximumLength(100).WithMessage("نام ماژول بیش از حد طولانی است.");
        RuleFor(x => x.Provider).MaximumLength(100).WithMessage("نام پرووایدر بیش از حد طولانی است.");
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Module) || !string.IsNullOrWhiteSpace(x.Provider))
            .WithMessage("حداقل ماژول یا پرووایدر را مشخص کنید.");
    }
}
public sealed class PurgeExternalMediaCacheEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapDelete("/external-cache", async ([FromQuery] string? module, [FromQuery] string? provider,
            ExternalMediaRegistry registry, ExternalMedia cache, ExternalMediaActivity activity, CancellationToken ct) =>
        {
            await new PurgeExternalMediaCacheValidator().ValidateAndThrowAsync(new(module, provider), ct);
            var count = await registry.PurgeAsync(module, provider, cache, ct);
            await activity.FlushAsync(ct);
            return Results.Ok(ApiResponseHelper.Success(new { MatchedMedia = count }, "پاک‌سازی کش انجام شد."));
        }).RequireAuthorization("AdminOnly").WithName("Media.PurgeExternalCache").WithTags("Media");
    }
}
