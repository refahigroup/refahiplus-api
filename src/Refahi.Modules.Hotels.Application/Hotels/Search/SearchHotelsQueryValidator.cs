//using FluentValidation;
//using Refahi.Modules.Hotels.Application.Contract.Providers.Queries;

//namespace Refahi.Modules.Hotels.Application.Hotels.Search;

//public sealed class SearchHotelsQueryValidator: AbstractValidator<SearchHotelsQuery>
//{
//    public SearchHotelsQueryValidator()
//    {
//        RuleFor(x => x.CityId).GreaterThan(0);
//        RuleFor(x => x.Adults).GreaterThan(0);
//        RuleFor(x => x.CheckIn).LessThan(x => x.CheckOut);
//    }
//}
