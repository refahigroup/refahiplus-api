using FluentValidation;
using Refahi.Modules.Hotels.Application.Contracts.Services.ProvisionalBooking;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;

namespace Refahi.Modules.Hotels.Application.ProvisionalBooking.Create;

public sealed class CreateProvisionalBookingCommandValidator: AbstractValidator<CreateProvisionalBookingCommand>
{
    public CreateProvisionalBookingCommandValidator()
    {
        RuleFor(x => x.HotelId).GreaterThan(0);
        RuleFor(x => x.RoomId).GreaterThan(0);
        RuleFor(x => x.RoomsCount).GreaterThan(0);

        RuleFor(x => x.BoardType)
            .NotEmpty()
            .Must(b => Enum.TryParse<RoomBoardType>(b, true, out _))
            .WithMessage("Invalid board type");

        RuleFor(x => x.CheckIn)
            .LessThan(x => x.CheckOut)
            .WithMessage("CheckIn must be before CheckOut");
    }
}
