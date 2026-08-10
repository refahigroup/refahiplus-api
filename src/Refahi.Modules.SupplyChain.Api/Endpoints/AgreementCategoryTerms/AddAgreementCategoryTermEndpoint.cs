using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementCategoryTerms;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.AgreementCategoryTerms;

public sealed class AddAgreementCategoryTermEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapPost(
                "/admin/agreements/{agreementId:guid}/agreement-category-terms",
                async (
                    Guid agreementId,
                    [FromBody] AgreementCategoryTermRequest body,
                    IMediator mediator,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await mediator.Send(
                        new AddAgreementCategoryTermCommand(
                            agreementId,
                            body.CategoryId,
                            body.AllowedSalesChannels,
                            body.CommissionPercent
                        ),
                        cancellationToken
                    );

                    return Results.Created(
                        $"/api/supply-chain/admin/agreements/{agreementId}/agreement-category-terms/{result.TermId}",
                        ApiResponseHelper.Success(
                            result,
                            "شرط دسته‌بندی قرارداد با موفقیت افزوده شد",
                            201
                        )
                    );
                }
            )
            .WithName("SupplyChain.AddAgreementCategoryTerm")
            .WithTags("SupplyChain.AgreementCategoryTerms")
            .RequireAuthorization("AdminOnly")
            .Produces<ApiResponse<AddAgreementCategoryTermResponse>>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record AgreementCategoryTermRequest(
    int CategoryId,
    short AllowedSalesChannels,
    decimal CommissionPercent
);
