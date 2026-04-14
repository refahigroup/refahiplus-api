using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Banners;

namespace Refahi.Modules.Store.Application.Features.Banners.CreateBanner;

public class CreateBannerCommandValidator : AbstractValidator<CreateBannerCommand>
{
    public CreateBannerCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان بنر الزامی است")
            .MaximumLength(200).WithMessage("عنوان بنر نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("آدرس تصویر الزامی است");

        RuleFor(x => x.BannerType)
            .InclusiveBetween((short)1, (short)3)
            .WithMessage("نوع بنر باید بین ۱ تا ۳ باشد");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش نمی‌تواند منفی باشد");
    }
}
