using FluentValidation;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CancelProviderBooking;

namespace Refahi.Modules.Hotels.Application.HotelRequests.CancelProviderBooking;

public sealed class CancelProviderBookingCommandValidator : AbstractValidator<CancelProviderBookingCommand>
{
    public CancelProviderBookingCommandValidator()
    {
        RuleFor(x => x.SagaId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(160);
    }
}
