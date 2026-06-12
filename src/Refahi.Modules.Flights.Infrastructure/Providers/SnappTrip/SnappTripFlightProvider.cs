using Microsoft.Extensions.Logging;
using Refahi.Modules.Flights.Application.Contracts.Providers;
using Refahi.Modules.Flights.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Logging;

namespace Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip;

internal sealed class SnappTripFlightProvider : IFlightProvider
{
    private readonly SnappTripFlightApiClient _apiClient;
    private readonly ILogger<SnappTripFlightProvider> _logger;

    public SnappTripFlightProvider(
        SnappTripFlightApiClient apiClient,
        ILogger<SnappTripFlightProvider> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<FlightSearchResponse> SearchAsync(
        FlightSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SnappTrip Flight search requested. Legs={LegCount}, Adult={Adult}, Child={Child}, Infant={Infant}",
            request.OriginDestinationInformations.Count,
            request.Adult,
            request.Child,
            request.Infant);

        var response = await _apiClient.SearchAsync(
            SnappTripFlightMapper.ToSnappTripRequest(request),
            cancellationToken);

        return SnappTripFlightMapper.ToFlightResponse(response.Data, response.MaskedRawPayload);
    }

    public async Task<FlightBookResponse> BookAsync(
        FlightBookRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SnappTrip Flight book requested. FareSourceCode={FareSourceCode}, PassengerCount={PassengerCount}",
            SnappTripFlightLogMasker.MaskToken(request.ProviderFareSourceCode),
            request.Passengers.Count);

        var response = await _apiClient.BookAsync(
            SnappTripFlightMapper.ToSnappTripRequest(request),
            cancellationToken);

        return SnappTripFlightMapper.ToFlightResponse(response.Data, response.MaskedRawPayload);
    }

    public async Task<FlightIssueResponse> IssueAsync(
        FlightIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SnappTrip Flight issue requested. BookId={BookId}",
            SnappTripFlightLogMasker.MaskToken(request.BookId));

        var response = await _apiClient.IssueAsync(
            SnappTripFlightMapper.ToSnappTripRequest(request),
            cancellationToken);

        return SnappTripFlightMapper.ToFlightResponse(response.Data, response.MaskedRawPayload);
    }

    public async Task<FlightInquiryResponse> InquiryAsync(
        FlightInquiryRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SnappTrip Flight inquiry requested. BookId={BookId}",
            SnappTripFlightLogMasker.MaskToken(request.BookId));

        var response = await _apiClient.InquiryAsync(request.BookId, cancellationToken);

        return SnappTripFlightMapper.ToFlightResponse(response.Data, response.MaskedRawPayload);
    }

    public async Task<FlightCancellationQuoteResponse> QuoteCancellationAsync(
        FlightCancellationQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SnappTrip Flight cancellation quote requested. TrackingCode={TrackingCode}, RouteId={RouteId}, TicketCount={TicketCount}",
            SnappTripFlightLogMasker.MaskToken(request.TrackingCode),
            request.RouteId,
            request.TicketSerials.Count);

        var response = await _apiClient.QuoteCancellationAsync(
            SnappTripFlightMapper.ToSnappTripRequest(request),
            cancellationToken);

        return SnappTripFlightMapper.ToFlightResponse(response.Data, response.MaskedRawPayload);
    }

    public async Task<FlightCancellationSubmitResponse> SubmitCancellationAsync(
        FlightCancellationSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SnappTrip Flight cancellation submit requested. TrackingCode={TrackingCode}, RouteId={RouteId}, TicketCount={TicketCount}",
            SnappTripFlightLogMasker.MaskToken(request.TrackingCode),
            request.RouteId,
            request.TicketSerials.Count);

        var response = await _apiClient.SubmitCancellationAsync(
            SnappTripFlightMapper.ToSnappTripRequest(request),
            cancellationToken);

        return SnappTripFlightMapper.ToFlightResponse(response.Data, response.MaskedRawPayload);
    }
}
