using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetProviderWalletChargesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("admin/provider/wallet-charges", async ([FromBody] ProviderReportBody body, ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetProviderWalletChargeReportQuery(
                body.PageNumber,
                body.FromDate,
                body.ToDate), ct))))
            .RequireAuthorization("AdminOnly")
            .WithName("Charge.Admin.ProviderWalletCharges")
            .WithTags("Charge.Admin")
            .Produces<ApiResponse<ProviderReportDto>>(StatusCodes.Status200OK);
    }
}
