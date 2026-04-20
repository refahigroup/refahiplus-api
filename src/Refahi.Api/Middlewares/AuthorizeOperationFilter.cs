using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Refahi.Api.Middlewares;

/// <summary>
/// Adds Bearer security requirement to every Swagger operation.
/// Swagger UI will then send the Authorization header for all requests.
/// Endpoints that don't require auth will simply ignore the token.
/// </summary>
public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
            }
        };
    }
}
