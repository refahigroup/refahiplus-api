using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Vouchers;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Vouchers;

public sealed record VoucherSourceCreateRequest(
    Guid SupplierId, string Title, VoucherSourceType SourceType,
    VoucherRedemptionMode RedemptionMode, int? DefaultValidityDays);
public sealed record VoucherSourceUpdateRequest(
    string Title, VoucherRedemptionMode RedemptionMode, int? DefaultValidityDays,
    uint ExpectedVersion);
public sealed record VoucherSourceActivationRequest(uint ExpectedVersion);
public sealed record VoucherCodeImportRequest(string IdempotencyKey, IReadOnlyList<VoucherCodeInput> Codes);
public sealed record VoucherCodePreviewRequest(IReadOnlyList<VoucherCodeInput> Codes);
public sealed record VoucherCodeDisableRequest(uint ExpectedVersion);
public sealed record ProductVoucherSourceRequest(Guid VoucherSourceId);
public sealed record VariantVoucherSourceRequest(Guid? VoucherSourceId);

public sealed class VoucherSourceEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        MapRole(routes, "vendor", false, "VendorOrAdmin");
        MapRole(routes, "admin", true, "AdminOnly");
    }

    private static void MapRole(IEndpointRouteBuilder r, string role, bool admin, string policy)
    {
        var prefix = $"/{role}/voucher-sources";
        var tag = $"Store.VoucherSources.{role}";
        r.MapGet(prefix, async (Guid supplierId, bool? includeInactive, HttpContext h,
            IMediator m, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(
                await m.Send(new ListVoucherSourcesQuery(Actor(h), admin, supplierId,
                    includeInactive ?? false), ct))))
            .WithName($"Store.{role}.ListVoucherSources").WithTags(tag).RequireAuthorization(policy);

        r.MapGet($"{prefix}/{{id:guid}}", async (Guid id, HttpContext h, IMediator m,
            CancellationToken ct) =>
        {
            var value = await m.Send(new GetVoucherSourceQuery(Actor(h), admin, id), ct);
            return value is null ? Results.NotFound(ApiResponseHelper.Error("منبع ووچر یافت نشد", statusCode: 404))
                : Results.Ok(ApiResponseHelper.Success(value));
        }).WithName($"Store.{role}.GetVoucherSource").WithTags(tag).RequireAuthorization(policy);

        r.MapPost(prefix, async (VoucherSourceCreateRequest b, HttpContext h, IMediator m,
            CancellationToken ct) => Results.Created(prefix, ApiResponseHelper.Success(
                await m.Send(new CreateVoucherSourceCommand(Actor(h), admin, b.SupplierId,
                    b.Title, b.SourceType, b.RedemptionMode, b.DefaultValidityDays), ct),
                "منبع ووچر ایجاد شد", 201)))
            .WithName($"Store.{role}.CreateVoucherSource").WithTags(tag).RequireAuthorization(policy);

        r.MapPut($"{prefix}/{{id:guid}}", async (Guid id, VoucherSourceUpdateRequest b,
            HttpContext h, IMediator m, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(
                await m.Send(new UpdateVoucherSourceCommand(Actor(h), admin, id, b.Title,
                    b.RedemptionMode, b.DefaultValidityDays, b.ExpectedVersion), ct),
                "منبع ووچر ویرایش شد")))
            .WithName($"Store.{role}.UpdateVoucherSource").WithTags(tag).RequireAuthorization(policy);

        foreach (var active in new[] { true, false })
        {
            var action = active ? "activate" : "deactivate";
            r.MapPost($"{prefix}/{{id:guid}}/{action}", async (Guid id,
                VoucherSourceActivationRequest b, HttpContext h, IMediator m, CancellationToken ct) =>
                Results.Ok(ApiResponseHelper.Success(await m.Send(
                    new SetVoucherSourceActivationCommand(Actor(h), admin, id, active,
                        b.ExpectedVersion), ct))))
                .WithName($"Store.{role}.{(active ? "Activate" : "Deactivate")}VoucherSource")
                .WithTags(tag).RequireAuthorization(policy);
        }

        r.MapPost($"{prefix}/{{id:guid}}/codes/preview", async (Guid id,
            VoucherCodePreviewRequest b, HttpContext h, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(
                new PreviewVoucherCodesCommand(Actor(h), admin, id, b.Codes), ct))))
            .WithName($"Store.{role}.PreviewVoucherCodes").WithTags(tag).RequireAuthorization(policy);

        r.MapPost($"{prefix}/{{id:guid}}/codes/import", async (Guid id,
            VoucherCodeImportRequest b, HttpContext h, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(
                new ImportVoucherCodesCommand(Actor(h), admin, id, b.IdempotencyKey, b.Codes), ct),
                "کدهای معتبر ثبت شدند")))
            .WithName($"Store.{role}.ImportVoucherCodes").WithTags(tag).RequireAuthorization(policy);

        r.MapGet($"{prefix}/{{id:guid}}/codes", async (Guid id, VoucherSourceCodeStatus? status,
            int? page, int? pageSize, HttpContext h, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(new GetVoucherSourceCodesQuery(
                Actor(h), admin, id, status, page ?? 1, pageSize ?? 50), ct))))
            .WithName($"Store.{role}.ListVoucherSourceCodes").WithTags(tag).RequireAuthorization(policy);

        r.MapPost($"{prefix}/{{id:guid}}/codes/{{codeId:guid}}/disable", async (Guid id,
            Guid codeId, VoucherCodeDisableRequest b, HttpContext h, IMediator m,
            CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await m.Send(
                new DisableVoucherSourceCodeCommand(Actor(h), admin, id, codeId,
                    b.ExpectedVersion), ct))))
            .WithName($"Store.{role}.DisableVoucherSourceCode").WithTags(tag).RequireAuthorization(policy);

        r.MapPut($"/{role}/products/{{productId:guid}}/voucher-source", async (Guid productId,
            ProductVoucherSourceRequest b, HttpContext h, IMediator m, CancellationToken ct) =>
        {
            await m.Send(new SetProductVoucherSourceCommand(Actor(h), admin, productId,
                b.VoucherSourceId), ct);
            return Results.Ok(ApiResponseHelper.Success("منبع ووچر محصول ذخیره شد"));
        }).WithName($"Store.{role}.SetProductVoucherSource").WithTags(tag).RequireAuthorization(policy);

        r.MapPut($"/{role}/products/{{productId:guid}}/variants/{{variantId:guid}}/voucher-source",
            async (Guid productId, Guid variantId, VariantVoucherSourceRequest b, HttpContext h,
                IMediator m, CancellationToken ct) =>
            {
                await m.Send(new SetProductVariantVoucherSourceCommand(Actor(h), admin,
                    productId, variantId, b.VoucherSourceId), ct);
                return Results.Ok(ApiResponseHelper.Success("منبع ووچر تنوع ذخیره شد"));
            }).WithName($"Store.{role}.SetProductVariantVoucherSource").WithTags(tag)
            .RequireAuthorization(policy);
    }

    private static Guid Actor(HttpContext h) => Guid.TryParse(
        h.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? h.User.FindFirstValue("sub"), out var id)
        ? id : Guid.Empty;
}
