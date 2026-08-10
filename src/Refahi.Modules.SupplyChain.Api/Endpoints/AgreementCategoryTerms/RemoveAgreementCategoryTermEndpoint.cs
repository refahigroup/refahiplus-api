using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementCategoryTerms;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.AgreementCategoryTerms;

public sealed class RemoveAgreementCategoryTermEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/admin/agreements/{agreementId:guid}/agreement-category-terms/{termId:guid}", async (
            Guid agreementId,
            Guid termId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(
                new RemoveAgreementCategoryTermCommand(agreementId, termId), cancellationToken);
            return Results.Ok(ApiResponseHelper.Success<object?>(null, "شرط دسته‌بندی قرارداد با موفقیت حذف شد"));
        })
        .WithName("SupplyChain.RemoveAgreementCategoryTerm")
        .WithTags("SupplyChain.AgreementCategoryTerms")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<object?>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
