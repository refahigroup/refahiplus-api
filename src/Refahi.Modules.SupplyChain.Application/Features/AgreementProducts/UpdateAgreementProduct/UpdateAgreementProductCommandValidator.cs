using FluentValidation;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementProducts;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementProducts.UpdateAgreementProduct;

public class UpdateAgreementProductCommandValidator : AbstractValidator<UpdateAgreementProductCommand>
{
    public UpdateAgreementProductCommandValidator()
    {
        RuleFor(x => x.AgreementId)
            .NotEmpty().WithMessage("شناسه قرارداد الزامی است");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام محصول الزامی است")
            .MaximumLength(200).WithMessage("نام محصول نباید بیشتر از ۲۰۰ کاراکتر باشد");

        When(x => !string.IsNullOrWhiteSpace(x.Description), () =>
        {
            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("توضیحات نباید بیشتر از ۱۰۰۰ کاراکتر باشد");
        });

        RuleFor(x => x.ProductType)
            .InclusiveBetween((short)1, (short)2).WithMessage("نوع محصول نامعتبر است");

        RuleFor(x => x.DeliveryType)
            .InclusiveBetween((short)1, (short)3).WithMessage("نوع تحویل نامعتبر است");

        RuleFor(x => x.SalesModel)
            .InclusiveBetween((short)1, (short)2).WithMessage("مدل فروش نامعتبر است");

        RuleFor(x => x.CommissionPercent)
            .InclusiveBetween(0m, 100m).WithMessage("درصد کمیسیون باید بین ۰ تا ۱۰۰ باشد");
    }
}
