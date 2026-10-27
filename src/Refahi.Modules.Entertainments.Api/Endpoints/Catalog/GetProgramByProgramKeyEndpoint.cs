using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Features.Catalog.GetProgram;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Catalog;

public sealed class GetProgramByProgramKeyEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet("/programs/{programKey:guid}",
            async(
                string programKey, 
                IMediator m,
                CancellationToken ct
            ) =>
            { 
                var value = await m.Send(new GetEntertainmentProgramQuery("touristpanel", programKey), ct);
                
                return value is null 
                    ? Results.NotFound(ApiResponseHelper.Error("محصول یافت نشد", statusCode: 404)) 
                    : Results.Ok(ApiResponseHelper.Success(value)); 
            })
            .WithName("Entertainment.GetProgram")
            .WithTags("Entertainment.Catalog");
    }
}

