using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Application.Features.Products.GetProducts;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class GetProductsQueryValidatorTests
{
    private readonly GetProductsQueryValidator _validator = new();

    [Theory]
    [InlineData("newest")]
    [InlineData("price-asc")]
    [InlineData("price-desc")]
    public void Valid_sort_is_accepted(string sort)
    {
        var result = _validator.Validate(new GetProductsQuery(1, null, sort, 1, 30));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Page_size_greater_than_thirty_is_rejected()
    {
        var result = _validator.Validate(new GetProductsQuery(1, null, "newest", 1, 31));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetProductsQuery.PageSize));
    }

    [Fact]
    public void Unknown_sort_is_rejected()
    {
        var result = _validator.Validate(new GetProductsQuery(1, null, "popular", 1, 30));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetProductsQuery.Sort));
    }

    [Fact]
    public void Search_longer_than_two_hundred_characters_is_rejected()
    {
        var result = _validator.Validate(new GetProductsQuery(1, new string('x', 201), "newest", 1, 30));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetProductsQuery.SearchQuery));
    }
}
