//using MediatR;
//using Microsoft.AspNetCore.Builder;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Routing;
//using Refahi.Contract.Presentation;
//using Refahi.Modules.Hotels.Application.Contract.Providers.DTOs;
//using Refahi.Modules.Hotels.Application.Contract.Providers.Queries;

//namespace Refahi.Modules.Hotels.Presentation.EndPoints.Hotels;

//public sealed class SearchHotelsEndpoint : IEndpoint
//{
//    public void Map(object app)
//    {
//        if (app is not IEndpointRouteBuilder routes)
//            return;

//        routes.MapGet("/search", async (
//            [FromQuery] int cityId,
//            [FromQuery] DateOnly checkin,
//            [FromQuery] DateOnly checkout,
//            [FromQuery] int? adults,
//            [FromQuery] int? children,
//            [FromQuery] int? availableRooms,
//            [FromQuery] int? minPrice,
//            [FromQuery] int? maxPrice,
//            [FromQuery] int[]? stars,
//            [FromQuery] string[]? accommodations,
//            [FromQuery] string searchSource,
//            ISender sender
//        ) =>
//        {
//            var query = new SearchHotelsQuery(
//                cityId,
//                checkin,
//                checkout,
//                adults,
//                children,
//                availableRooms,
//                minPrice,
//                maxPrice,
//                stars,
//                accommodations
//            );

//            var result = await sender.Send(query);

//            return Results.Ok(result);

//        })
//        .Produces<IEnumerable<HotelSearchByCityResultDto>>()
//        .WithName("Hotels.Search")
//        .WithTags("Hotels");
//    }
//}