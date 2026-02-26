using Refahi.Shared.Presentation;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace Refahi.Api.Middlewares;

/// <summary>
/// Middleware to wrap all successful responses in unified ApiResponse format
/// Captures response body, wraps it, and returns to client
/// </summary>
public sealed class ResponseWrappingMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        // Allow non-ASCII characters (Persian, Arabic, etc.) to be written as-is
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public ResponseWrappingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to API endpoints (avoid swagger, static files, UI)
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) || context.Request.Path.StartsWithSegments("/api/swagger"))
        {
            await _next(context);
            return;
        }

        // Store original response stream
        var originalBodyStream = context.Response.Body;

        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            // Only wrap successful responses (2xx status codes)
            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                await WrapResponseAsync(context, responseBody, originalBodyStream, context.Response.StatusCode);
            }
            else
            {
                // For non-2xx responses, write original response
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    /// <summary>
    /// Wrap response data in unified format
    /// </summary>
    private static async Task WrapResponseAsync(HttpContext context, MemoryStream responseBody, Stream originalBodyStream, int statusCode)
    {
        responseBody.Seek(0, SeekOrigin.Begin);
        var responseData = await new StreamReader(responseBody).ReadToEndAsync();

        // Only process JSON responses
        if (!context.Response.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
            return;
        }

        // Handle empty body (e.g., 204 No Content) — respond with unified envelope
            if (string.IsNullOrWhiteSpace(responseData))
        {
            // Create empty success envelope for 204 or other successful but empty responses
            var emptyWrapped = ApiResponseHelper.Success<object>(null, "عملیات با موفقیت انجام شد", statusCode);
                context.Response.ContentType = "application/json; charset=utf-8";
                var wrappedJsonEmpty = JsonSerializer.Serialize(emptyWrapped, JsonOptions);
                var wrappedBytesEmpty = Encoding.UTF8.GetBytes(wrappedJsonEmpty);
                await originalBodyStream.WriteAsync(wrappedBytesEmpty, 0, wrappedBytesEmpty.Length);
            return;
        }

        // Check if response is already in ApiResponse format
        if (IsAlreadyWrapped(responseData))
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
            return;
        }

        // Parse original response as JSON
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(responseData);

        // Create wrapped response based on status code
        var wrappedResponse = statusCode switch
        {
            StatusCodes.Status200OK => ApiResponseHelper.Success(jsonElement, "درخواست با موفقیت انجام شد", StatusCodes.Status200OK),
            StatusCodes.Status201Created => ApiResponseHelper.Success(jsonElement, "منبع با موفقیت ایجاد شد", StatusCodes.Status201Created),
            StatusCodes.Status204NoContent => ApiResponseHelper.Success(jsonElement, "عملیات با موفقیت انجام شد", StatusCodes.Status204NoContent),
            _ => ApiResponseHelper.Success(jsonElement, "درخواست با موفقیت انجام شد", statusCode),
        };

        // Write wrapped response
        context.Response.ContentType = "application/json; charset=utf-8";
        var wrappedJson = JsonSerializer.Serialize(wrappedResponse, JsonOptions);
        var wrappedBytes = Encoding.UTF8.GetBytes(wrappedJson);

        await originalBodyStream.WriteAsync(wrappedBytes, 0, wrappedBytes.Length);
    }

    /// <summary>
    /// Check if response is already wrapped in ApiResponse format
    /// </summary>
    private static bool IsAlreadyWrapped(string responseData)
    {
        if (string.IsNullOrWhiteSpace(responseData))
            return false;

        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(responseData);
            // Check if response has "success" and "data" properties (ApiResponse structure)
            return json.TryGetProperty("success", out _) &&
                   json.TryGetProperty("data", out _);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Extension methods for response wrapping middleware
/// </summary>
public static class ResponseWrappingMiddlewareExtensions
{
    /// <summary>
    /// Register response wrapping middleware
    /// Should be registered after exception middleware but before routing
    /// </summary>
    public static IApplicationBuilder UseResponseWrappingMiddleware(this IApplicationBuilder builder)
        => builder.UseMiddleware<ResponseWrappingMiddleware>();
}

