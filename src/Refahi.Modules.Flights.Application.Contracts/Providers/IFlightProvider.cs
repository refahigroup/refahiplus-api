using Refahi.Modules.Flights.Application.Contracts.Providers.DTOs;

namespace Refahi.Modules.Flights.Application.Contracts.Providers;

public interface IFlightProvider
{
    Task<FlightSearchResponse> SearchAsync(
        FlightSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<FlightBookResponse> BookAsync(
        FlightBookRequest request,
        CancellationToken cancellationToken = default);

    Task<FlightIssueResponse> IssueAsync(
        FlightIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<FlightInquiryResponse> InquiryAsync(
        FlightInquiryRequest request,
        CancellationToken cancellationToken = default);

    Task<FlightCancellationQuoteResponse> QuoteCancellationAsync(
        FlightCancellationQuoteRequest request,
        CancellationToken cancellationToken = default);

    Task<FlightCancellationSubmitResponse> SubmitCancellationAsync(
        FlightCancellationSubmitRequest request,
        CancellationToken cancellationToken = default);
}
