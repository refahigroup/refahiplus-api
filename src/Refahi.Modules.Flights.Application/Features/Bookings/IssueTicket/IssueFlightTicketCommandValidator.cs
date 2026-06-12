using FluentValidation;

namespace Refahi.Modules.Flights.Application.Features.Bookings.IssueTicket;

public sealed class IssueFlightTicketCommandValidator : AbstractValidator<IssueFlightTicketCommand>
{
    public IssueFlightTicketCommandValidator()
    {
        RuleFor(command => command.BookingId)
            .NotEmpty()
            .WithMessage("شناسه رزرو پرواز الزامی است.");

        RuleFor(command => command.UserId)
            .NotEmpty()
            .WithMessage("شناسه کاربر الزامی است.");

        RuleFor(command => command.CallerRole)
            .NotEmpty()
            .WithMessage("نقش کاربر الزامی است.");

        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("کلید یکتاسازی الزامی است.");
    }
}
