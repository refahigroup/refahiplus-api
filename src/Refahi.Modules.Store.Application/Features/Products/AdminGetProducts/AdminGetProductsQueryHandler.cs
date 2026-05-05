using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Products.AdminGetProducts;

public class AdminGetProductsQueryHandler : IRequestHandler<AdminGetProductsQuery, AdminProductsPagedResponse>
{
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IMediator _mediator;
    private readonly IPathService _pathService;

    public AdminGetProductsQueryHandler(IProductRepository productRepo, IShopProductRepository shopProductRepo, IMediator mediator, IPathService pathService)
    {
        _productRepo = productRepo;
        _shopProductRepo = shopProductRepo;
        _mediator = mediator;
        _pathService = pathService;
    }

    public async Task<AdminProductsPagedResponse> Handle(
        AdminGetProductsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _productRepo.GetPagedAdminAsync(
            request.ShopId,
            request.IsDeleted,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtoTasks = items.Select(async p =>
        {
            var ap = await _mediator.Send(new GetAgreementProductByIdQuery(p.AgreementProductId), cancellationToken);
            var sp = request.ShopId.HasValue
                ? await _shopProductRepo.GetAsync(request.ShopId.Value, p.Id, cancellationToken)
                : (await _shopProductRepo.GetByProductAsync(p.Id, isActive: true, 1, 1, cancellationToken)).Items.FirstOrDefault();
            var mainImage = p.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? p.Images.FirstOrDefault()?.ImageUrl;
            var mainImageUrl = mainImage is null ? null : _pathService.MakeAbsoluteMediaUrl(mainImage);
            return new ProductSummaryDto(
                p.Id, p.Title, p.Slug,
                sp?.Price ?? 0,
                sp?.DiscountedPrice ?? 0,
                ap is not null ? ((ProductType)ap.ProductType).ToString() : string.Empty,
                ap is not null ? ((DeliveryType)ap.DeliveryType).ToString() : string.Empty,
                ap is not null ? ((SalesModel)ap.SalesModel).ToString() : string.Empty,
                mainImageUrl,
                p.IsAvailable,
                ap?.CommissionPercent ?? 0);
        });

        var dtos = await Task.WhenAll(dtoTasks);
        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        return new AdminProductsPagedResponse(dtos, request.PageNumber, request.PageSize, total, totalPages);
    }
}
