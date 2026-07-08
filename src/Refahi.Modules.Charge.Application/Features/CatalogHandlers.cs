using FluentValidation;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Application.Services;

namespace Refahi.Modules.Charge.Application.Features;

public sealed class GetProductsValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsValidator()
        => RuleFor(x => x.Operator).IsInEnum().WithMessage("اپراتور معتبر نیست");
}

public sealed class GetOffersValidator : AbstractValidator<GetOffersQuery>
{
    public GetOffersValidator()
    {
        RuleFor(x => x.Operator).IsInEnum().WithMessage("اپراتور معتبر نیست");
        RuleFor(x => x.MobileNumber)
            .Matches("^09[0-9]{9}$")
            .WithMessage("شماره موبایل معتبر نیست");
        RuleFor(x => x.Category).IsInEnum().WithMessage("دسته پیشنهاد معتبر نیست");
    }
}

public sealed class GetPostpaidBalanceValidator : AbstractValidator<GetPostpaidBalanceQuery>
{
    public GetPostpaidBalanceValidator()
    {
        RuleFor(x => x.Operator).IsInEnum().WithMessage("اپراتور معتبر نیست");
        RuleFor(x => x.MobileNumber)
            .Matches("^09[0-9]{9}$")
            .WithMessage("شماره موبایل معتبر نیست");
    }
}

public sealed class GetPinCategoriesValidator : AbstractValidator<GetPinCategoriesQuery>
{
    public GetPinCategoriesValidator()
        => RuleFor(x => x.Operator!.Value)
            .IsInEnum()
            .When(x => x.Operator.HasValue)
            .WithMessage("اپراتور معتبر نیست");
}

public sealed class CatalogHandlers :
    IRequestHandler<GetOperatorsQuery, IReadOnlyList<ChargeOperatorDto>>,
    IRequestHandler<GetProductsQuery, IReadOnlyList<ChargeProductDto>>,
    IRequestHandler<GetOffersQuery, IReadOnlyList<ChargeProductDto>>,
    IRequestHandler<CheckEligibilityQuery, ChargeEligibilityDto>,
    IRequestHandler<GetPostpaidBalanceQuery, ChargePostpaidBalanceDto>,
    IRequestHandler<GetPinCategoriesQuery, IReadOnlyList<PinChargeCategoryDto>>,
    IRequestHandler<GetPackageTypesQuery, IReadOnlyList<PackageTypeDto>>
{
    private readonly IChargeProviderResolver _providers;
    public CatalogHandlers(IChargeProviderResolver providers) => _providers = providers;
    public Task<IReadOnlyList<ChargeOperatorDto>> Handle(GetOperatorsQuery q, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ChargeOperatorDto>>([
            new(1, "irancell", "ایرانسل", ChargeCapabilityPolicy.For(ChargeOperator.Irancell)),
            new(2, "mci", "همراه اول", ChargeCapabilityPolicy.For(ChargeOperator.Mci)),
            new(3, "rightel", "رایتل", ChargeCapabilityPolicy.For(ChargeOperator.Rightel)),
            new(4, "shatel", "شاتل", ChargeCapabilityPolicy.For(ChargeOperator.Shatel)),
            new(5, "taliya", "تالیا", ChargeCapabilityPolicy.For(ChargeOperator.Taliya))]);
    public Task<IReadOnlyList<ChargeProductDto>> Handle(GetProductsQuery q, CancellationToken ct) => _providers.GetDefault().GetProductsAsync(q.Operator, ct);
    public Task<IReadOnlyList<ChargeProductDto>> Handle(GetOffersQuery q, CancellationToken ct) => _providers.GetDefault().GetOffersAsync(q.Operator, q.MobileNumber, q.Category, ct);
    public Task<ChargeEligibilityDto> Handle(CheckEligibilityQuery q, CancellationToken ct)
        => _providers.GetDefault().CheckEligibilityAsync(new(q.Operator, q.MobileNumber, q.AmountMinor, q.ProviderProductId, q.ProductCategory), ct);
    public Task<ChargePostpaidBalanceDto> Handle(GetPostpaidBalanceQuery q, CancellationToken ct) => _providers.GetDefault().GetPostpaidBalanceAsync(q.Operator, q.MobileNumber, ct);
    public async Task<IReadOnlyList<PinChargeCategoryDto>> Handle(GetPinCategoriesQuery q, CancellationToken ct)
    {
        var categories = await _providers.GetDefault().GetPinCategoriesAsync(ct);
        return q.Operator.HasValue ? categories.Where(x => x.Operator == q.Operator.Value).ToArray() : categories;
    }
    public Task<IReadOnlyList<PackageTypeDto>> Handle(GetPackageTypesQuery q, CancellationToken ct) => _providers.GetDefault().GetPackageTypesAsync(ct);
}
