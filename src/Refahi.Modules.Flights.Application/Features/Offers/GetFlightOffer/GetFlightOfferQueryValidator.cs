using FluentValidation;

namespace Refahi.Modules.Flights.Application.Features.Offers.GetFlightOffer;

public sealed class GetFlightOfferQueryValidator : AbstractValidator<GetFlightOfferQuery>
{
    public GetFlightOfferQueryValidator()
    {
        RuleFor(query => query.OfferToken)
            .NotEmpty()
            .WithMessage("توکن پیشنهاد پرواز الزامی است.")
            .MaximumLength(120)
            .WithMessage("توکن پیشنهاد پرواز معتبر نیست.");
    }
}
