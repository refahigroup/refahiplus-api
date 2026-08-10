using FluentValidation;
using System.Linq.Expressions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementCategoryTerms;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementCategoryTerms;

internal static class AgreementCategoryTermValidationRules
{
    public static void AddRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, Guid>> agreementId,
        Expression<Func<T, int>> categoryId,
        Expression<Func<T, short>> channels,
        Expression<Func<T, decimal>> commission)
    {
        validator.RuleFor(agreementId).NotEmpty().WithMessage("شناسه قرارداد الزامی است");
        validator.RuleFor(categoryId).GreaterThan(0).WithMessage("شناسه دسته‌بندی الزامی است");
        validator.RuleFor(channels)
            .Must(value => value is (short)SalesChannel.Online
                or (short)SalesChannel.InPerson
                or (short)(SalesChannel.Online | SalesChannel.InPerson))
            .WithMessage("کانال فروش باید آنلاین، حضوری یا هر دو باشد");
        validator.RuleFor(commission)
            .InclusiveBetween(0m, 100m).WithMessage("درصد کمیسیون باید بین ۰ تا ۱۰۰ باشد")
            .PrecisionScale(5, 2, false).WithMessage("درصد کمیسیون حداکثر دو رقم اعشار دارد");
    }
}

public sealed class AddAgreementCategoryTermCommandValidator : AbstractValidator<AddAgreementCategoryTermCommand>
{
    public AddAgreementCategoryTermCommandValidator() => AgreementCategoryTermValidationRules.AddRules(
        this, x => x.AgreementId, x => x.CategoryId, x => x.AllowedSalesChannels, x => x.CommissionPercent);
}

public sealed class UpdateAgreementCategoryTermCommandValidator : AbstractValidator<UpdateAgreementCategoryTermCommand>
{
    public UpdateAgreementCategoryTermCommandValidator()
    {
        AgreementCategoryTermValidationRules.AddRules(
            this, x => x.AgreementId, x => x.CategoryId, x => x.AllowedSalesChannels, x => x.CommissionPercent);
        RuleFor(x => x.TermId).NotEmpty().WithMessage("شناسه شرط دسته‌بندی قرارداد الزامی است");
    }
}

public sealed class RemoveAgreementCategoryTermCommandValidator : AbstractValidator<RemoveAgreementCategoryTermCommand>
{
    public RemoveAgreementCategoryTermCommandValidator()
    {
        RuleFor(x => x.AgreementId).NotEmpty().WithMessage("شناسه قرارداد الزامی است");
        RuleFor(x => x.TermId).NotEmpty().WithMessage("شناسه شرط دسته‌بندی قرارداد الزامی است");
    }
}
