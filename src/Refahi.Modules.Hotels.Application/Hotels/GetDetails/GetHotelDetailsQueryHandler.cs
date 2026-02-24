using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Hotels.Application.Contracts.Providers.Queries;

namespace Refahi.Modules.Hotels.Application.Hotels.GetDetails
{
    public sealed class GetHotelDetailsQueryHandler: IRequestHandler<GetHotelDetailsQuery, IEnumerable<HotelDetailsDto>>
    {
        private readonly IHotelProvider _provider;

        public GetHotelDetailsQueryHandler(IHotelProvider provider)
        {
            _provider = provider;
        }

        public async Task<IEnumerable<HotelDetailsDto>> Handle(GetHotelDetailsQuery request, CancellationToken cancellationToken)
        {
            return await _provider.GetHotelDetailsAsync(request);
        }
    }
}
