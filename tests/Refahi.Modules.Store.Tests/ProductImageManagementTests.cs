using Refahi.Modules.Store.Application.Contracts.Products;
using Refahi.Modules.Store.Application.Features.Catalog;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class ProductImageManagementTests
{
    [Fact]
    public void Adding_main_image_clears_previous_main_image()
    {
        var product = CreateProduct();

        product.AddImage("/media/first.webp", true, 0);
        product.AddImage("/media/second.webp", true, 1);

        Assert.False(product.Images[0].IsMain);
        Assert.True(product.Images[1].IsMain);
    }

    [Fact]
    public void Adding_duplicate_image_is_rejected()
    {
        var product = CreateProduct();
        product.AddImage("/media/product.webp");

        var exception = Assert.Throws<StoreDomainException>(() =>
            product.AddImage("/MEDIA/PRODUCT.WEBP")
        );

        Assert.Equal("IMAGE_ALREADY_EXISTS", exception.ErrorCode);
    }

    [Fact]
    public void Reordering_unknown_image_is_rejected()
    {
        var product = CreateProduct();
        product.AddImage("/media/product.webp");

        var exception = Assert.Throws<StoreDomainException>(() =>
            product.ReorderImages([(123, 0)])
        );

        Assert.Equal("IMAGE_NOT_FOUND", exception.ErrorCode);
    }

    [Fact]
    public void Image_command_validators_reject_invalid_payloads()
    {
        var actorId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var add = new AddCatalogProductImageCommand(actorId, true, productId, "", false, -1);
        var reorder = new ReorderCatalogProductImagesCommand(
            actorId,
            true,
            productId,
            [new ProductImageOrderInput(1, 0), new ProductImageOrderInput(1, 1)]
        );

        Assert.False(new AddProductImageValidator().Validate(add).IsValid);
        Assert.False(new ReorderProductImagesValidator().Validate(reorder).IsValid);
    }

    private static Product CreateProduct() =>
        Product.CreateCatalogProduct(
            Guid.NewGuid(),
            1,
            ProductType.Goods,
            SalesModel.InventoryBased,
            FulfillmentMethod.Shipping,
            "محصول تست",
            $"test-product-{Guid.NewGuid():N}"
        );
}
