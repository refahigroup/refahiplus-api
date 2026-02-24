//using MediatR;
//using Refahi.Modules.Hotels.Application.Contract.Providers;
//using Refahi.Modules.Hotels.Application.Contract.Providers.DTOs.Availability.AvailabilityByCity;

//namespace Refahi.Modules.Hotels.Application.Hotels.Search
//{
//    public sealed class SearchHotelsQueryHandler: IRequestHandler<GetAvailabilityByCityQuery, GetAvailabilityByCityDto>
//    {
//        private readonly IHotelProvider _provider;

//        public SearchHotelsQueryHandler(IHotelProvider provider)
//        {
//            _provider = provider;
//        }

//        public async Task<GetAvailabilityByCityDto> Handle(GetAvailabilityByCityQuery request, CancellationToken cancellationToken)
//        {
//            return await _provider.GetAvailabilityByCity(request);
//        }
//    }
//}
