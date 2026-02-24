using FluentValidation;
using Refahi.Modules.Hotels.Application.Contracts.Providers.Queries;

namespace Refahi.Modules.Hotels.Application.Hotels.GetDetails
{
    public sealed class GetHotelDetailsQueryValidator: AbstractValidator<GetHotelDetailsQuery>
    {
        public GetHotelDetailsQueryValidator()
        {
            RuleFor(x => x.HotelId).GreaterThan(0);
        }
    }
}
