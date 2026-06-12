using MediatR;
using Refahi.Modules.Flights.Application.Features.Offers;

namespace Refahi.Modules.Flights.Application.Features.Offers.GetFlightOffer;

public sealed record GetFlightOfferQuery(string OfferToken) : IRequest<FlightOfferDto?>;
