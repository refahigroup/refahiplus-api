using FluentValidation;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;
using Refahi.Modules.Orders.Domain.Exceptions;
using Refahi.Modules.References.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.SupplyChain.Domain.Exceptions;
using Refahi.Shared.Presentation;
using System.Net;

namespace Refahi.Api.Middlewares;

public sealed class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error occurred");
            await HandleValidationExceptionAsync(context, ex);
        }
        catch (StoreDomainException ex)
        {
            _logger.LogWarning(ex, "Store domain rule violation");
            await HandleDomainExceptionAsync(context, ex.Message);
        }
        catch (OrderDomainException ex)
        {
            _logger.LogWarning(ex, "Order domain rule violation");
            await HandleDomainExceptionAsync(context, ex.Message);
        }
        catch (ReferencesDomainException ex)
        {
            _logger.LogWarning(ex, "References domain rule violation");
            await HandleDomainExceptionAsync(context, ex.Message);
        }
        catch (SupplyChainDomainException ex)
        {
            _logger.LogWarning(ex, "SupplyChain domain rule violation");
            await HandleDomainExceptionAsync(context, ex.Message);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain rule violation");
            await HandleDomainExceptionAsync(context, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Handle FluentValidation exceptions
    /// </summary>
    private static Task HandleValidationExceptionAsync(HttpContext context, ValidationException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

        var errors = exception.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ErrorMessage).ToArray()
            );

        var response = ApiResponseHelper.ValidationError(errors);
        return context.Response.WriteAsJsonAsync(response);
    }

    /// <summary>
    /// Handle domain exceptions — returns 400 Bad Request with the domain message
    /// </summary>
    private static Task HandleDomainExceptionAsync(HttpContext context, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        var response = ApiResponseHelper.Error(message, statusCode: (int)HttpStatusCode.BadRequest);
        return context.Response.WriteAsJsonAsync(response);
    }

    /// <summary>
    /// Handle general exceptions
    /// </summary>
    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = exception switch
        {
            ArgumentNullException => ApiResponseHelper.Error(
                "پارامتر مورد نیاز ارائه نشده است",
                statusCode: (int)HttpStatusCode.BadRequest
            ),
            ArgumentException => ApiResponseHelper.Error(
                exception.Message,
                statusCode: (int)HttpStatusCode.BadRequest
            ),
            UnauthorizedAccessException => ApiResponseHelper.Error(
                "دسترسی غیرمجاز است",
                statusCode: (int)HttpStatusCode.Unauthorized
            ),
            _ => ApiResponseHelper.Error(
                "خطایی در سرور رخ داد. لطفا بعدا دوباره تلاش کنید",
                traceId: context.TraceIdentifier,
                statusCode: (int)HttpStatusCode.InternalServerError
            )
        };

        return context.Response.WriteAsJsonAsync(response);
    }
}

/// <summary>
/// Extension methods for API exception middleware
/// </summary>
public static class ApiExceptionMiddlewareExtensions
{
    /// <summary>
    /// Register global exception handling middleware
    /// Must be called early in middleware pipeline
    /// </summary>
    public static IApplicationBuilder UseApiExceptionMiddleware(this IApplicationBuilder builder)
        => builder.UseMiddleware<ApiExceptionMiddleware>();
}
