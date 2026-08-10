using FluentValidation;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementCategoryTerms;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementCategoryTerms.ResolveAgreementCategoryTerms;

public sealed class ResolveAgreementCategoryTermQueryValidator
    : AbstractValidator<ResolveAgreementCategoryTermQuery>
{
    public ResolveAgreementCategoryTermQueryValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("شناسه تامین‌کننده الزامی است");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("شناسه دسته‌بندی الزامی است");
        RuleFor(x => x.SalesChannel)
            .Must(x => x is (short)SalesChannel.Online or (short)SalesChannel.InPerson)
            .WithMessage("کانال فروش باید آنلاین یا حضوری باشد");
    }
}

public sealed class ResolveAgreementCategoryTermsBatchQueryValidator
    : AbstractValidator<ResolveAgreementCategoryTermsBatchQuery>
{
    public ResolveAgreementCategoryTermsBatchQueryValidator()
    {
        RuleFor(x => x.Requests)
            .NotNull()
            .WithMessage("درخواست‌های حل قرارداد الزامی است")
            .Must(x => x.Count <= 1000)
            .WithMessage("حداکثر ۱۰۰۰ درخواست در هر batch مجاز است");

        RuleForEach(x => x.Requests)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.SupplierId)
                    .NotEmpty()
                    .WithMessage("شناسه تامین‌کننده الزامی است");
                item.RuleFor(x => x.CategoryId)
                    .GreaterThan(0)
                    .WithMessage("شناسه دسته‌بندی الزامی است");
                item.RuleFor(x => x.SalesChannel)
                    .Must(x => x is (short)SalesChannel.Online or (short)SalesChannel.InPerson)
                    .WithMessage("کانال فروش باید آنلاین یا حضوری باشد");
            });
    }
}
