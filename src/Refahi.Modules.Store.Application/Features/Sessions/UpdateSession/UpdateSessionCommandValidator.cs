using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Sessions;

namespace Refahi.Modules.Store.Application.Features.Sessions.UpdateSession;

public class UpdateSessionCommandValidator : AbstractValidator<UpdateSessionCommand>
{
    public UpdateSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("شناسه سانس الزامی است");

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("ظرفیت سانس باید بیشتر از صفر باشد");

        RuleFor(x => x.PriceAdjustment)
            .GreaterThanOrEqualTo(0).WithMessage("تفاوت قیمت نمی‌تواند منفی باشد");
    }
}
