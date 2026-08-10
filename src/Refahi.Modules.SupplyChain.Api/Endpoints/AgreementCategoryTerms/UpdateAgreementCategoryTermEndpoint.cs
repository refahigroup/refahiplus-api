using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementCategoryTerms;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.AgreementCategoryTerms;

public sealed class UpdateAgreementCategoryTermEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapPut(
                "/admin/agreements/{agreementId:guid}/agreement-category-terms/{termId:guid}",
                async (
                    Guid agreementId,
                    Guid termId,
                    [FromBody] AgreementCategoryTermRequest body,
                    IMediator mediator,
                    CancellationToken cancellationToken
                ) =>
                {
                    await mediator.Send(
                        new UpdateAgreementCategoryTermCommand(
                            agreementId,
                            termId,
                            body.CategoryId,
                            body.AllowedSalesChannels,
                            body.CommissionPercent
                        ),
                        cancellationToken
                    );
                    return Results.Ok(
                        ApiResponseHelper.Success<object?>(
                            null,
                            "شرط دسته‌بندی قرارداد با موفقیت به‌روزرسانی شد"
                        )
                    );
                }
            )
            .WithName("SupplyChain.UpdateAgreementCategoryTerm")
            .WithTags("SupplyChain.AgreementCategoryTerms")
            .RequireAuthorization("AdminOnly")
            .Produces<ApiResponse<object?>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}
