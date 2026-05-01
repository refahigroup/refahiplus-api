using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Features.Products.CreateProduct;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, CreateProductResponse>
{
    private readonly IProductRepository _productRepo;
    private readonly IMediator _mediator;

    public CreateProductCommandHandler(IProductRepository productRepo, IMediator mediator)
    {
        _productRepo = productRepo;
        _mediator = mediator;
    }

    public async Task<CreateProductResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Validate the AgreementProduct exists and is not deleted
        var agreementProduct = await _mediator.Send(
            new GetAgreementProductByIdQuery(request.AgreementProductId), cancellationToken);

        if (agreementProduct is null || agreementProduct.IsDeleted)
            throw new StoreDomainException("قرارداد محصول یافت نشد یا غیرفعال است", "AGREEMENT_PRODUCT_NOT_FOUND");

        if (await _productRepo.SlugExistsAsync(request.Slug.Trim().ToLower(), cancellationToken))
            throw new StoreDomainException("این اسلاگ قبلاً ثبت شده است", "SLUG_ALREADY_EXISTS");

        var product = Product.Create(
            request.AgreementProductId,
            request.Title,
            request.Slug,
            request.Description,
            request.StockCount);

        await _productRepo.AddAsync(product, cancellationToken);

        return new CreateProductResponse(product.Id, product.Title, product.Slug);
    }
}
