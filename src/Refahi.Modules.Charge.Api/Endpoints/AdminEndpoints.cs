using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class AdminEndpoints : IEndpoint
{
    public void Map(object app)
    {

        if (app is not IEndpointRouteBuilder r) 
            return;

        var admin = r.MapGroup("admin").RequireAuthorization("AdminOnly");

        admin.MapGet("markup-rules", async (ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetMarkupRulesQuery(), ct))))
            .WithName("Charge.Admin.MarkupRules.Get").WithTags("Charge.Admin").Produces<ApiResponse<IReadOnlyList<MarkupRuleDto>>>(StatusCodes.Status200OK);

        admin.MapPost("markup-rules", async ([FromBody] MarkupRuleBody b, ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new UpsertMarkupRuleCommand(null, b.Operator, b.ServiceType, b.Percent, b.FixedAmountMinor, b.EffectiveFrom, b.EffectiveTo), ct), "قانون افزایش قیمت ثبت شد")))
            .WithName("Charge.Admin.MarkupRules.Create").WithTags("Charge.Admin").Produces<ApiResponse<MarkupRuleDto>>(StatusCodes.Status200OK);

        admin.MapPut("markup-rules/{id:guid}", async (Guid id, [FromBody] MarkupRuleBody b, ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new UpsertMarkupRuleCommand(id, b.Operator, b.ServiceType, b.Percent, b.FixedAmountMinor, b.EffectiveFrom, b.EffectiveTo), ct), "قانون افزایش قیمت ویرایش شد")))
            .WithName("Charge.Admin.MarkupRules.Update").WithTags("Charge.Admin").Produces<ApiResponse<MarkupRuleDto>>(StatusCodes.Status200OK);

        admin.MapPost("markup-rules/{id:guid}/deactivate", async (Guid id, ISender s, CancellationToken ct) =>
        { await s.Send(new DeactivateMarkupRuleCommand(id), ct); return Results.Ok(ApiResponseHelper.Success(new ChargeOperationResponse(id), "قانون افزایش قیمت غیرفعال شد")); })
            .WithName("Charge.Admin.MarkupRules.Deactivate").WithTags("Charge.Admin").Produces<ApiResponse<ChargeOperationResponse>>(StatusCodes.Status200OK);

        admin.MapGet("provider/balance", async (ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetProviderBalanceQuery(), ct))))
            .WithName("Charge.Admin.ProviderBalance").WithTags("Charge.Admin").Produces<ApiResponse<Refahi.Modules.Charge.Application.Contracts.Providers.ProviderBalanceDto>>(StatusCodes.Status200OK);

        admin.MapGet("provider/errors", async (ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetProviderErrorsQuery(), ct))))
            .WithName("Charge.Admin.ProviderErrors").WithTags("Charge.Admin").Produces<ApiResponse<IReadOnlyList<Refahi.Modules.Charge.Application.Contracts.Providers.ProviderErrorDto>>>(StatusCodes.Status200OK);

        admin.MapGet("provider/channels", async (ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetProviderChannelsQuery(), ct))))
            .WithName("Charge.Admin.ProviderChannels").WithTags("Charge.Admin").Produces<ApiResponse<IReadOnlyList<Refahi.Modules.Charge.Application.Contracts.Providers.ProviderChannelDto>>>(StatusCodes.Status200OK);

        admin.MapPost("provider/transactions", async ([FromBody] ProviderReportBody b, ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetProviderTransactionReportQuery(b.PageNumber, b.FromDate, b.ToDate), ct))))
            .WithName("Charge.Admin.ProviderTransactions").WithTags("Charge.Admin").Produces<ApiResponse<Refahi.Modules.Charge.Application.Contracts.Providers.ProviderReportDto>>(StatusCodes.Status200OK);

        admin.MapPost("provider/wallet-charges", async ([FromBody] ProviderReportBody b, ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetProviderWalletChargeReportQuery(b.PageNumber, b.FromDate, b.ToDate), ct))))
            .WithName("Charge.Admin.ProviderWalletCharges").WithTags("Charge.Admin").Produces<ApiResponse<Refahi.Modules.Charge.Application.Contracts.Providers.ProviderReportDto>>(StatusCodes.Status200OK);

        admin.MapPost("charge-requests/{id:guid}/reconcile", async (Guid id, [FromQuery] bool force, ISender s, CancellationToken ct) =>
        { await s.Send(new ReconcileChargeRequestCommand(id, force), ct); return Results.Ok(ApiResponseHelper.Success(new ChargeOperationResponse(id), "بازبینی تراکنش انجام شد")); })
            .WithName("Charge.Admin.Reconcile").WithTags("Charge.Admin").Produces<ApiResponse<ChargeOperationResponse>>(StatusCodes.Status200OK);
    }
}
public sealed record MarkupRuleBody(ChargeOperator? Operator, ChargeServiceType? ServiceType, decimal Percent,
    long FixedAmountMinor, DateTime EffectiveFrom, DateTime? EffectiveTo);

public sealed record ProviderReportBody(int PageNumber = 1, DateOnly? FromDate = null, DateOnly? ToDate = null);
