using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Offers;
using Refahi.Modules.Store.Application.Contracts.Products;
using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Application.Features.Catalog;

public sealed class CreateProductValidator : AbstractValidator<CreateCatalogProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("شناسه تامین‌کننده الزامی است");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("دسته‌بندی محصول نامعتبر است");
        RuleFor(x => x.EligibilityChannel)
            .Must(x => x == 0 || Enum.IsDefined(typeof(SalesChannel), x))
            .WithMessage("کانال فروش نامعتبر است");
        RuleFor(x => x.ProductType)
            .Must(x => Enum.IsDefined(typeof(ProductType), x))
            .WithMessage("نوع محصول نامعتبر است");
        RuleFor(x => x.SalesModel)
            .Must(x => Enum.IsDefined(typeof(SalesModel), x))
            .WithMessage("مدل فروش نامعتبر است");
        RuleFor(x => x.FulfillmentMethod)
            .Must(x => Enum.IsDefined(typeof(FulfillmentMethod), x))
            .WithMessage("روش تحویل نامعتبر است");
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("عنوان محصول الزامی است")
            .MaximumLength(300)
            .WithMessage("عنوان محصول حداکثر ۳۰۰ کاراکتر است");
        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithMessage("اسلاگ الزامی است")
            .MaximumLength(300)
            .WithMessage("اسلاگ حداکثر ۳۰۰ کاراکتر است")
            .Matches("^[a-z0-9-]+$")
            .WithMessage("اسلاگ نامعتبر است");
        RuleFor(x => x.Description)
            .MaximumLength(5000)
            .WithMessage("توضیحات حداکثر ۵۰۰۰ کاراکتر است");
    }
}

public sealed class UpdateProductValidator : AbstractValidator<UpdateCatalogProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("شناسه محصول الزامی است");
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("عنوان محصول الزامی است")
            .MaximumLength(300)
            .WithMessage("عنوان محصول حداکثر ۳۰۰ کاراکتر است");
        RuleFor(x => x.Description)
            .MaximumLength(5000)
            .WithMessage("توضیحات حداکثر ۵۰۰۰ کاراکتر است");
    }
}

public sealed class CreateOfferValidator : AbstractValidator<CreateOfferCommand>
{
    public CreateOfferValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("شناسه محصول الزامی است");
        RuleFor(x => x.ShopId).NotEmpty().WithMessage("شناسه فروشگاه الزامی است");
        RuleFor(x => x.OriginalPriceMinor)
            .GreaterThan(0)
            .WithMessage("قیمت اصلی باید بیشتر از صفر باشد");
        RuleFor(x => x.DiscountPercent)
            .InclusiveBetween(0, 100)
            .WithMessage("درصد تخفیف باید بین صفر تا صد باشد");
        RuleFor(x => x.DiscountPercent)
            .Must(HasAtMostTwoDecimalPlaces)
            .WithMessage("درصد تخفیف حداکثر می‌تواند دو رقم اعشار داشته باشد");
        RuleFor(x => x)
            .Must(x => !x.EndDateUtc.HasValue || x.StartDateUtc < x.EndDateUtc.Value)
            .WithMessage("زمان پایان باید بعد از زمان شروع باشد");
    }

    internal static bool HasAtMostTwoDecimalPlaces(decimal value) =>
        ((decimal.GetBits(value)[3] >> 16) & 0xFF) <= 2;
}

public sealed class UpdateOfferValidator : AbstractValidator<UpdateOfferCommand>
{
    public UpdateOfferValidator()
    {
        RuleFor(x => x.OfferId)
            .NotEmpty()
            .WithMessage("شناسه پیشنهاد الزامی است");

        RuleFor(x => x.OriginalPriceMinor)
            .GreaterThan(0)
            .WithMessage("قیمت اصلی باید بیشتر از صفر باشد");

        RuleFor(x => x.DiscountPercent)
            .InclusiveBetween(0, 100)
            .WithMessage("درصد تخفیف باید بین صفر تا صد باشد");

        RuleFor(x => x.DiscountPercent)
            .Must(CreateOfferValidator.HasAtMostTwoDecimalPlaces)
            .WithMessage("درصد تخفیف حداکثر می‌تواند دو رقم اعشار داشته باشد");

        RuleFor(x => x)
            .Must(x => !x.EndDateUtc.HasValue || x.StartDateUtc < x.EndDateUtc.Value)
            .WithMessage("زمان پایان باید بعد از زمان شروع باشد");
    }
}

public sealed class ListOffersValidator : AbstractValidator<ListOffersQuery>
{
    public ListOffersValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("شماره صفحه نامعتبر است");

        RuleFor(x => x.PageSize)
            .LessThan(101)
            .WithMessage("اندازه صفحه باید بین ۱ تا ۱۰۰ باشد")
            .GreaterThan(0)
            .WithMessage("اندازه صفحه باید بین ۱ تا ۱۰۰ باشد");
    }
}

public sealed class ListProductsValidator : AbstractValidator<ListCatalogProductsQuery>
{
    public ListProductsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("شماره صفحه نامعتبر است");

        RuleFor(x => x.PageSize)
            .LessThan(101)
            .WithMessage("اندازه صفحه باید بین ۱ تا ۱۰۰ باشد")
            .GreaterThan(0)
            .WithMessage("اندازه صفحه باید بین ۱ تا ۱۰۰ باشد");
    }
}

public sealed class CreateProductVariantValidator
    : AbstractValidator<CreateCatalogProductVariantCommand>
{
    public CreateProductVariantValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.Combinations)
            .NotNull()
            .WithMessage("ترکیب تنوع الزامی است");

        RuleFor(x => x.StockCount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("موجودی نمی‌تواند منفی باشد");

        RuleFor(x => x.CapacityType)
            .IsInEnum()
            .WithMessage("نوع ظرفیت نامعتبر است");

        RuleFor(x => x.Capacity)
            .GreaterThan(0)
            .When(x =>
                x.CapacityType
                    is VariantCapacityType.TotalPeriod
                        or VariantCapacityType.PerEligibleDay
            )
            .WithMessage("ظرفیت باید بیشتر از صفر باشد");

        RuleFor(x => x)
            .Must(x =>
                x.FromDate.HasValue == x.ToDate.HasValue
                && (!x.FromDate.HasValue || x.FromDate <= x.ToDate)
            )
            .WithMessage("بازه اعتبار تنوع نامعتبر است");
    }
}

public sealed class UpdateProductVariantValidator
    : AbstractValidator<UpdateCatalogProductVariantCommand>
{
    public UpdateProductVariantValidator()
    {
        Include(new CreateProductVariantValidatorAdapter());

        RuleFor(x => x.VariantId)
            .NotEmpty()
            .WithMessage("شناسه تنوع الزامی است");
    }

    internal sealed class CreateProductVariantValidatorAdapter
        : AbstractValidator<UpdateCatalogProductVariantCommand>
    {
        public CreateProductVariantValidatorAdapter()
        {
            RuleFor(x => x.ProductId).NotEmpty().WithMessage("شناسه محصول الزامی است");
            RuleFor(x => x.Combinations).NotNull().WithMessage("ترکیب تنوع الزامی است");
            RuleFor(x => x.StockCount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("موجودی نمی‌تواند منفی باشد");
            RuleFor(x => x.CapacityType).IsInEnum().WithMessage("نوع ظرفیت نامعتبر است");
            RuleFor(x => x.Capacity)
                .GreaterThan(0)
                .When(x =>
                    x.CapacityType
                        is VariantCapacityType.TotalPeriod
                            or VariantCapacityType.PerEligibleDay
                )
                .WithMessage("ظرفیت باید بیشتر از صفر باشد");
            RuleFor(x => x)
                .Must(x =>
                    x.FromDate.HasValue == x.ToDate.HasValue
                    && (!x.FromDate.HasValue || x.FromDate <= x.ToDate)
                )
                .WithMessage("بازه اعتبار تنوع نامعتبر است");
        }
    }
}

public sealed class CreateProductSessionValidator
    : AbstractValidator<CreateCatalogProductSessionCommand>
{
    public CreateProductSessionValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("شناسه محصول الزامی است");
        RuleFor(x => x.Date).NotEmpty().WithMessage("تاریخ سانس الزامی است");
        RuleFor(x => x.StartTime).NotEmpty().WithMessage("زمان شروع الزامی است");
        RuleFor(x => x.EndTime).NotEmpty().WithMessage("زمان پایان الزامی است");
        RuleFor(x => x.Capacity).GreaterThan(0).WithMessage("ظرفیت باید بیشتر از صفر باشد");
    }
}

public sealed class UpdateProductSessionValidator
    : AbstractValidator<UpdateCatalogProductSessionCommand>
{
    public UpdateProductSessionValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("شناسه محصول الزامی است");
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("شناسه سانس الزامی است");
        RuleFor(x => x.Capacity).GreaterThan(0).WithMessage("ظرفیت باید بیشتر از صفر باشد");
    }
}

public sealed class GetPublicProductCatalogValidator
    : AbstractValidator<GetPublicProductCatalogQuery>
{
    public GetPublicProductCatalogValidator()
    {
        RuleFor(x => x.ModuleSlug)
            .NotEmpty()
            .WithMessage("اسلاگ ماژول الزامی است")
            .MaximumLength(200)
            .WithMessage("اسلاگ ماژول نامعتبر است");

        RuleFor(x => x.Search).MaximumLength(200).WithMessage("عبارت جستجو حداکثر ۲۰۰ کاراکتر است");
        RuleFor(x => x.ShopSlug).MaximumLength(300).WithMessage("اسلاگ فروشگاه نامعتبر است");
        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .When(x => x.CategoryId.HasValue)
            .WithMessage("دسته‌بندی نامعتبر است");
        RuleFor(x => x.MinPriceMinor)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinPriceMinor.HasValue)
            .WithMessage("حداقل قیمت نامعتبر است");
        RuleFor(x => x.MaxPriceMinor)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxPriceMinor.HasValue)
            .WithMessage("حداکثر قیمت نامعتبر است");
        RuleFor(x => x)
            .Must(x =>
                !x.MinPriceMinor.HasValue
                || !x.MaxPriceMinor.HasValue
                || x.MinPriceMinor <= x.MaxPriceMinor
            )
            .WithMessage("بازه قیمت نامعتبر است");

        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("شماره صفحه نامعتبر است");

        RuleFor(x => x.PageSize)
            .LessThan(101)
            .WithMessage("اندازه صفحه باید بین ۱ تا ۱۰۰ باشد")
            .GreaterThan(0)
            .WithMessage("اندازه صفحه باید بین ۱ تا ۱۰۰ باشد");
    }
}

public sealed class GetPublicProductDetailValidator
    : AbstractValidator<GetPublicProductDetailQuery>
{
    public GetPublicProductDetailValidator()
    {
        RuleFor(x => x.ModuleSlug)
            .NotEmpty()
            .WithMessage("اسلاگ ماژول الزامی است")
            .MaximumLength(200)
            .WithMessage("اسلاگ ماژول نامعتبر است");
        RuleFor(x => x.ProductSlug)
            .NotEmpty()
            .WithMessage("اسلاگ محصول الزامی است")
            .MaximumLength(300)
            .WithMessage("اسلاگ محصول نامعتبر است");
        RuleFor(x => x.ShopSlug).MaximumLength(300).WithMessage("اسلاگ فروشگاه نامعتبر است");
    }
}
