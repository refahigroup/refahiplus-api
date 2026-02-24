using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.Statics.Cities;

public record GetCitiesRequest(string? CityName) : IRequest<IEnumerable<GetCitiesResponse>>;