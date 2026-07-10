using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetProviderTransactionsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("admin/provider/transactions", async ([FromBody] ProviderReportBody body, ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetProviderTransactionReportQuery(
                body.PageNumber,
                body.FromDate,
                body.ToDate), ct))))
            .RequireAuthorization("AdminOnly")
            .WithName("Charge.Admin.ProviderTransactions")
            .WithTags("Charge.Admin")
            .Produces<ApiResponse<ProviderReportDto>>(StatusCodes.Status200OK);
    }
}
