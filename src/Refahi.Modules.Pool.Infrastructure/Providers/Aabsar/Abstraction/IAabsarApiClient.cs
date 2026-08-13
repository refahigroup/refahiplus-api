using Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;
using System.Text.Json;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Abstraction;

// ============================================================================
// API client contract
// ============================================================================

public interface IAabsarApiClient
{
    /// <summary>
    /// GET / - verifies that the API is reachable and the access token is valid.
    /// </summary>
    Task<AabsarHealthResponse> HealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /showtimes - returns active showtimes from now up to 10 days ahead.
    /// </summary>
    Task<AabsarApiResponse<IReadOnlyList<AabsarShowtimeDto>>> GetShowtimesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /check-showtime - verifies existence/activity and returns current capacity.
    /// </summary>
    Task<AabsarApiResponse<AabsarShowtimeCapacityDto>> CheckShowtimeAsync(
        AabsarCheckShowtimeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /order-created - creates a completed order and returns ticket codes.
    /// </summary>
    Task<AabsarApiResponse<AabsarCreateOrderResultDto>> CreateOrderAsync(
        AabsarCreateOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /cancel-tickets - cancels eligible unused adult/child tickets.
    /// A successful response has data = null.
    /// </summary>
    Task<AabsarApiResponse<JsonElement?>> CancelTicketsAsync(
        AabsarCancelTicketsRequest request,
        CancellationToken cancellationToken = default);
}

