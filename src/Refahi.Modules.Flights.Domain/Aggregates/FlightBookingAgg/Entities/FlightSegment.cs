using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Entities;

public sealed class FlightSegment
{
    private FlightSegment()
    {
        ProviderSegmentId = string.Empty;
        FlightNumber = string.Empty;
        AirlineCode = string.Empty;
        AirlineName = string.Empty;
        OriginAirportCode = string.Empty;
        OriginCaption = string.Empty;
        DestinationAirportCode = string.Empty;
        DestinationCaption = string.Empty;
    }

    public FlightSegment(
        int sequence,
        string providerSegmentId,
        string flightNumber,
        string airlineCode,
        string airlineName,
        string originAirportCode,
        string originCaption,
        string destinationAirportCode,
        string destinationCaption,
        DateTime departureAtUtc,
        DateTime arrivalAtUtc)
    {
        if (sequence <= 0)
        {
            throw new DomainException("Flight segment sequence must be greater than zero.");
        }

        if (arrivalAtUtc <= departureAtUtc)
        {
            throw new DomainException("Flight segment arrival must be after departure.");
        }

        ProviderSegmentId = Require(providerSegmentId, "Provider segment id is required.");
        FlightNumber = Require(flightNumber, "Flight number is required.");
        AirlineCode = Require(airlineCode, "Airline code is required.").ToUpperInvariant();
        AirlineName = Require(airlineName, "Airline name is required.");
        OriginAirportCode = Require(originAirportCode, "Origin airport code is required.").ToUpperInvariant();
        OriginCaption = Require(originCaption, "Origin caption is required.");
        DestinationAirportCode = Require(destinationAirportCode, "Destination airport code is required.").ToUpperInvariant();
        DestinationCaption = Require(destinationCaption, "Destination caption is required.");
        Sequence = sequence;
        DepartureAtUtc = departureAtUtc;
        ArrivalAtUtc = arrivalAtUtc;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public int Sequence { get; private set; }
    public string ProviderSegmentId { get; private set; }
    public string FlightNumber { get; private set; }
    public string AirlineCode { get; private set; }
    public string AirlineName { get; private set; }
    public string OriginAirportCode { get; private set; }
    public string OriginCaption { get; private set; }
    public string DestinationAirportCode { get; private set; }
    public string DestinationCaption { get; private set; }
    public DateTime DepartureAtUtc { get; private set; }
    public DateTime ArrivalAtUtc { get; private set; }

    private static string Require(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(message);
        }

        return value.Trim();
    }
}
