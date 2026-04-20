using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.CreateProduct;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, CreateProductResponse>
{
    private readonly IProductRepository _productRepo;
    private readonly IStoreCategoryRepository _categoryRepo;

    public CreateProductCommandHandler(IProductRepository productRepo, IStoreCategoryRepository categoryRepo)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
    }

    public async Task<CreateProductResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (await _productRepo.SlugExistsAsync(request.Slug.Trim().ToLower(), cancellationToken))
            throw new StoreDomainException("این اسلاگ قبلاً ثبت شده است", "SLUG_ALREADY_EXISTS");

        var category = await _categoryRepo.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new StoreDomainException("دسته‌بندی یافت نشد", "CATEGORY_NOT_FOUND");

        if (!string.Equals(category.CategoryCode, request.CategoryCode, StringComparison.OrdinalIgnoreCase))
            throw new StoreDomainException(
                $"CategoryCode ورودی با دسته‌بندی انتخاب‌شده مطابقت ندارد. مقدار صحیح: '{category.CategoryCode}'",
                "CATEGORY_CODE_MISMATCH");

        var product = Product.Create(
            request.ShopId,
            request.Title,
            request.Slug,
            request.PriceMinor,
            (ProductType)request.ProductType,
            (DeliveryType)request.DeliveryType,
            (Domain.Enums.SalesModel)request.SalesModel,
            request.CategoryId,
            request.CategoryCode,
            request.CommissionPercent,
            request.Description,
            request.StockCount,
            request.CityId,
            city: null,
            request.Area);

        await _productRepo.AddAsync(product, cancellationToken);

        return new CreateProductResponse(product.Id, product.Title, product.Slug);
    }
}
