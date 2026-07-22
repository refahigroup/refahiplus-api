using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Queries.Categories;
using Refahi.Modules.Store.Application.Features.Categories.GetStoreCategories;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class GetStoreCategoriesQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Returns_pruned_preorder_tree_with_local_parent_metadata()
    {
        var root = Category(10, "ریشه", "root", "store", 999, 5);
        var first = Category(11, "الف", "first", "store.first", 10, 1,
            [Category(13, "برگ", "leaf", "store.first.leaf", 11, 3)]);
        var empty = Category(12, "بدون محصول", "empty", "store.empty", 10, 2);
        var second = Category(14, "ب", "second", "store.second", 10, 1);
        var outside = Category(99, "خارج", "outside", "outside", null, 0);

        var leafAgreementProductId = Guid.NewGuid();
        var secondAgreementProductId = Guid.NewGuid();
        var outsideAgreementProductId = Guid.NewGuid();
        var agreementProducts = new Dictionary<Guid, AgreementProductDto>
        {
            [leafAgreementProductId] = AgreementProduct(leafAgreementProductId, 13),
            [secondAgreementProductId] = AgreementProduct(secondAgreementProductId, 14),
            [outsideAgreementProductId] = AgreementProduct(outsideAgreementProductId, 99)
        };

        var handler = CreateHandler(
            StoreModule.Create("فروشگاه", "market", categoryId: 10),
            agreementProducts,
            [leafAgreementProductId, secondAgreementProductId, outsideAgreementProductId],
            root,
            [empty, first, second, outside]);

        var result = await handler.Handle(new GetStoreCategoriesQuery(1), CancellationToken.None);

        Assert.Equal([10, 11, 13, 14], result.Select(category => category.Id));
        Assert.Null(result[0].ParentId);
        Assert.Null(result[0].ParentTitle);
        Assert.Equal(10, result[1].ParentId);
        Assert.Equal("ریشه", result[1].ParentTitle);
        Assert.Equal(11, result[2].ParentId);
        Assert.Equal("الف", result[2].ParentTitle);
        Assert.DoesNotContain(result, category => category.Id is 12 or 99);
    }

    [Fact]
    public async Task Returns_empty_when_module_has_no_category_or_no_eligible_offer()
    {
        var noCategoryHandler = CreateHandler(
            StoreModule.Create("فروشگاه", "market"),
            new Dictionary<Guid, AgreementProductDto>(),
            [],
            null,
            []);

        var withoutCategory = await noCategoryHandler.Handle(
            new GetStoreCategoriesQuery(1), CancellationToken.None);

        var root = Category(10, "ریشه", "root", "store", null, 0);
        var agreementProductId = Guid.NewGuid();
        var noOfferHandler = CreateHandler(
            StoreModule.Create("فروشگاه", "market-two", categoryId: 10),
            new Dictionary<Guid, AgreementProductDto>
            {
                [agreementProductId] = AgreementProduct(agreementProductId, 10)
            },
            [],
            root,
            []);

        var withoutOffer = await noOfferHandler.Handle(
            new GetStoreCategoriesQuery(1), CancellationToken.None);

        Assert.Empty(withoutCategory);
        Assert.Empty(withoutOffer);
    }

    private static GetStoreCategoriesQueryHandler CreateHandler(
        StoreModule module,
        IReadOnlyDictionary<Guid, AgreementProductDto> agreementProducts,
        IReadOnlyList<Guid> eligibleAgreementProductIds,
        CategoryDto? root,
        IReadOnlyList<CategoryDto> descendants)
        => new(
            new FakeModuleRepository(module),
            new FakeContextService(agreementProducts),
            new FakeOfferRepository(eligibleAgreementProductIds),
            new FakeClock(),
            new FakeMediator(root, descendants));

    private static CategoryDto Category(
        int id,
        string name,
        string slug,
        string code,
        int? parentId,
        int sortOrder,
        List<CategoryDto>? children = null)
        => new(id, name, slug, code, $"/{slug}.png", parentId, sortOrder, true, children);

    private static AgreementProductDto AgreementProduct(Guid id, int categoryId)
        => new(id, Guid.NewGuid(), "محصول", null, categoryId, null, 1, 1, 1, 0, false, Now);

    private sealed class FakeModuleRepository(StoreModule module) : IStoreModuleRepository
    {
        public Task<StoreModule?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<StoreModule?>(module);
        public Task<StoreModule?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<StoreModule>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(string slug, int? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(StoreModule value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(StoreModule value, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeContextService(IReadOnlyDictionary<Guid, AgreementProductDto> products)
        : ISyntheticOfferQueryContextService
    {
        public Task<SyntheticOfferQueryContext> ResolveAsync(
            int moduleId, int? categoryId, Guid? shopId, string? shopSlug,
            string? salesModel, CancellationToken ct)
            => Task.FromResult(new SyntheticOfferQueryContext(
                true,
                null,
                products.Keys.ToHashSet(),
                products,
                products.Keys.ToArray(),
                []));
    }

    private sealed class FakeOfferRepository(IReadOnlyList<Guid> eligibleIds) : ISyntheticOfferReadRepository
    {
        public Task<IReadOnlyList<Guid>> GetEligibleAgreementProductIdsAsync(SyntheticOfferQuerySpec spec, CancellationToken ct = default)
            => Task.FromResult(eligibleIds);
        public Task<(IReadOnlyList<SyntheticOfferReadModel> Items, int Total)> GetOffersAsync(SyntheticOfferQuerySpec spec, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<SyntheticProductCatalogReadModel> Items, int Total)> GetProductCatalogAsync(SyntheticOfferQuerySpec spec, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SyntheticOfferReadModel>> GetProductOffersAsync(SyntheticOfferQuerySpec spec, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeClock : IStoreBusinessClock
    {
        public StoreBusinessMoment Current => new(new DateOnly(2026, 7, 22), new TimeOnly(10, 0));
    }

    private sealed class FakeMediator(CategoryDto? root, IReadOnlyList<CategoryDto> descendants) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object? response = request switch
            {
                GetCategoryByIdQuery => root,
                GetCategoriesQuery => descendants.ToList(),
                _ => throw new NotSupportedException(request.GetType().FullName)
            };
            return Task.FromResult((TResponse)response!);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
