using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Services.Statics.Cities;

namespace Refahi.Modules.Hotels.Application.Statics.Cities;

public class GetCitiesQueryHandler : IRequestHandler<GetCitiesRequest, IEnumerable<GetCitiesResponse>>
{
    private readonly IHotelProvider _provider;

    public GetCitiesQueryHandler(IHotelProvider provider)
    {
        _provider = provider;
    }


    public async Task<IEnumerable<GetCitiesResponse>> Handle(GetCitiesRequest request, CancellationToken cancellationToken)
    {
        return await _provider.GetAllCities(request.CityName);
    }
}
