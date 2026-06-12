using MediatR;

namespace Refahi.Modules.Flights.Application.Features.Airports.GetFlightAirports;

public sealed class GetFlightAirportsQueryHandler
    : IRequestHandler<GetFlightAirportsQuery, GetFlightAirportsResponse>
{
    private static readonly IReadOnlyCollection<FlightAirportDto> Airports =
    [
        new("THR", "THR", "تهران", "Tehran", "مهرآباد", "Mehrabad International Airport", "IR", true),
        new("IKA", "THR", "تهران", "Tehran", "امام خمینی", "Imam Khomeini International Airport", "IR", true),
        new("MHD", "MHD", "مشهد", "Mashhad", "شهید هاشمی‌نژاد", "Shahid Hasheminejad International Airport", "IR", true),
        new("SYZ", "SYZ", "شیراز", "Shiraz", "شهید دستغیب", "Shahid Dastgheib International Airport", "IR", true),
        new("KIH", "KIH", "کیش", "Kish", "فرودگاه بین‌المللی کیش", "Kish International Airport", "IR", true),
        new("IFN", "IFN", "اصفهان", "Isfahan", "شهید بهشتی", "Shahid Beheshti International Airport", "IR", true),
        new("AWZ", "AWZ", "اهواز", "Ahvaz", "قاسم سلیمانی", "Ahvaz International Airport", "IR", true),
        new("TBZ", "TBZ", "تبریز", "Tabriz", "شهید مدنی", "Shahid Madani International Airport", "IR", true),
        new("GSM", "GSM", "قشم", "Qeshm", "دیرستان", "Qeshm International Airport", "IR", true),
        new("BND", "BND", "بندرعباس", "Bandar Abbas", "بندرعباس", "Bandar Abbas International Airport", "IR", true),
        new("KER", "KER", "کرمان", "Kerman", "آیت‌الله هاشمی رفسنجانی", "Kerman International Airport", "IR", false),
        new("RAS", "RAS", "رشت", "Rasht", "سردار جنگل", "Sardar-e Jangal Airport", "IR", false)
    ];

    public Task<GetFlightAirportsResponse> Handle(
        GetFlightAirportsQuery request,
        CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim();

        var airports = string.IsNullOrWhiteSpace(query)
            ? Airports.OrderByDescending(airport => airport.IsPopular).ThenBy(airport => airport.CityNameEn)
            : Airports.Where(airport => Matches(airport, query))
                .OrderByDescending(airport => airport.IsPopular)
                .ThenBy(airport => airport.CityNameEn);

        return Task.FromResult(new GetFlightAirportsResponse(airports.Take(20).ToList()));
    }

    private static bool Matches(FlightAirportDto airport, string query)
    {
        return airport.Code.Contains(query, StringComparison.OrdinalIgnoreCase)
               || airport.CityNameFa.Contains(query, StringComparison.OrdinalIgnoreCase)
               || airport.CityNameEn.Contains(query, StringComparison.OrdinalIgnoreCase)
               || airport.AirportNameFa.Contains(query, StringComparison.OrdinalIgnoreCase)
               || airport.AirportNameEn.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
