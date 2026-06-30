using FluentValidation;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.ConvertHotelRequestToOrder;

namespace Refahi.Modules.Hotels.Application.HotelRequests.ConvertHotelRequestToOrder;

public sealed class ConvertHotelRequestToOrderCommandValidator : AbstractValidator<ConvertHotelRequestToOrderCommand>
{
    public ConvertHotelRequestToOrderCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("شناسه درخواست هتل الزامی است");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("کاربر الزامی است");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200).WithMessage("کلید تکرارپذیری الزامی است");
    }
}
