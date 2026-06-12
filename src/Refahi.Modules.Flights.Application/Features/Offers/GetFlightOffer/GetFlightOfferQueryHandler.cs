using System.Text.Json;
using MediatR;
using Refahi.Modules.Flights.Application.Features.Offers;
using Refahi.Modules.Flights.Domain.Repositories;

namespace Refahi.Modules.Flights.Application.Features.Offers.GetFlightOffer;

public sealed class GetFlightOfferQueryHandler
    : IRequestHandler<GetFlightOfferQuery, FlightOfferDto?>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IFlightOfferSnapshotRepository _offerSnapshotRepository;

    public GetFlightOfferQueryHandler(IFlightOfferSnapshotRepository offerSnapshotRepository)
    {
        _offerSnapshotRepository = offerSnapshotRepository;
    }

    public async Task<FlightOfferDto?> Handle(
        GetFlightOfferQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _offerSnapshotRepository.GetByTokenAsync(
            request.OfferToken.Trim(),
            cancellationToken);

        if (snapshot is null || snapshot.IsExpired(DateTime.UtcNow))
            return null;

        return JsonSerializer.Deserialize<FlightOfferDto>(
            snapshot.PublicOfferSnapshotJson,
            JsonOptions);
    }
}
