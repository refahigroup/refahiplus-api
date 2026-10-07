using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Cinema.Api.Endpoints;

public class LandingEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet(
                "/cinema/landing",
                async (IMediator mediator, CancellationToken ct) =>
                {
                    var result = new {
                        Name = "Refahi Cinema",
                        Description = "Welcome to Refahi Cinema API",    
                    };

                    return Results.Ok(ApiResponseHelper.Success(result, "بنر با موفقیت فعال شد"));
                }
            )
            .WithName("Cinema.GetLanding")
            .WithTags("Cinema.Landing")
            .RequireAuthorization("AdminOnly")
            .Produces<ApiResponse<LandingResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }
}

public class LandingResponse
{
    public string Name { get; set; }
    public string Description { get; set; }
}