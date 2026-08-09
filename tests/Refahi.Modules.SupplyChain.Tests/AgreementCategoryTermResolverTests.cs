using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementCategoryTerms;
using Refahi.Modules.SupplyChain.Application.Features.AgreementCategoryTerms.ResolveAgreementCategoryTerms;
using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Entities;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Tests;

public sealed class AgreementCategoryTermResolverTests
{
    private static readonly Guid SupplierId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset At = new(2026, 8, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Exact_category_beats_parent_and_multiple_ancestors()
    {
        var exact = Candidate(categoryId: 3, commission: 30);
        var result = await ResolveAsync([Candidate(1, 10), Candidate(2, 20), exact]);

        Assert.Equal(exact.TermId, result!.TermId);
        Assert.Equal(3, result.MatchedCategoryId);
        Assert.Equal(3, result.RequestedCategoryId);
    }

    [Fact]
    public async Task Parent_category_covers_descendant_when_no_more_specific_term_exists()
    {
        var parent = Candidate(categoryId: 2, commission: 20);
        var result = await ResolveAsync([Candidate(1, 10), parent]);
        Assert.Equal(parent.TermId, result!.TermId);
    }

    [Fact]
    public async Task Overlapping_agreements_use_newest_from_date_then_created_at_then_id_ascending()
    {
        var oldestAgreement = Candidate(3, 1) with { AgreementFromDate = At.AddDays(-20) };
        var newestAgreement = Candidate(3, 2) with { AgreementFromDate = At.AddDays(-10) };
        Assert.Equal(newestAgreement.TermId, (await ResolveAsync([oldestAgreement, newestAgreement]))!.TermId);

        var createdAt = At.AddDays(-2);
        var olderTerm = Candidate(3, 3) with { AgreementFromDate = At.AddDays(-10), TermCreatedAt = createdAt.AddHours(-1) };
        var newerTerm = Candidate(3, 4) with { AgreementFromDate = At.AddDays(-10), TermCreatedAt = createdAt };
        Assert.Equal(newerTerm.TermId, (await ResolveAsync([olderTerm, newerTerm]))!.TermId);

        var lowerId = Candidate(3, 5) with
        {
            TermId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            AgreementFromDate = At.AddDays(-10),
            TermCreatedAt = createdAt
        };
        var higherId = lowerId with { TermId = Guid.Parse("00000000-0000-0000-0000-000000000002") };
        Assert.Equal(lowerId.TermId, (await ResolveAsync([higherId, lowerId]))!.TermId);
    }

    [Theory]
    [InlineData((short)SalesChannel.Online)]
    [InlineData((short)SalesChannel.InPerson)]
    public async Task Both_flag_contains_online_and_in_person(short requestedChannel)
    {
        var candidate = Candidate(3, 10) with
        {
            AllowedSalesChannels = SalesChannel.Online | SalesChannel.InPerson
        };
        Assert.NotNull(await ResolveAsync([candidate], requestedChannel));
    }

    [Fact]
    public async Task Missing_channel_does_not_resolve()
    {
        var candidate = Candidate(3, 10) with { AllowedSalesChannels = SalesChannel.InPerson };
        Assert.Null(await ResolveAsync([candidate], (short)SalesChannel.Online));
    }

    [Fact]
    public async Task Validity_is_from_inclusive_and_to_exclusive()
    {
        var candidate = Candidate(3, 10) with
        {
            AgreementFromDate = At,
            AgreementToDate = At.AddHours(1)
        };
        Assert.NotNull(await ResolveAsync([candidate], at: At));
        Assert.NotNull(await ResolveAsync([candidate], at: At.AddHours(1).AddTicks(-1)));
        Assert.Null(await ResolveAsync([candidate], at: At.AddHours(1)));
    }

    [Theory]
    [InlineData(AgreementStatus.Registered, false, false)]
    [InlineData(AgreementStatus.UnderReview, false, false)]
    [InlineData(AgreementStatus.Rejected, false, false)]
    [InlineData(AgreementStatus.Approved, true, false)]
    [InlineData(AgreementStatus.Approved, false, true)]
    public async Task Rejected_or_deleted_agreement_and_deleted_term_do_not_resolve(
        AgreementStatus status, bool agreementDeleted, bool termDeleted)
    {
        var candidate = Candidate(3, 10) with
        {
            AgreementStatus = status,
            AgreementIsDeleted = agreementDeleted,
            TermIsDeleted = termDeleted
        };
        Assert.Null(await ResolveAsync([candidate]));
    }

    [Fact]
    public async Task Expired_agreement_does_not_resolve()
    {
        var candidate = Candidate(3, 10) with { AgreementToDate = At };
        Assert.Null(await ResolveAsync([candidate]));
    }

    [Fact]
    public async Task Batch_uses_one_hierarchy_read_and_one_candidate_read_and_preserves_order()
    {
        var repository = new FakeAgreementRepository([Candidate(2, 20), Candidate(3, 30)]);
        var categories = new FakeCategoryTreeHandler();
        using var provider = BuildProvider(repository, categories);
        var mediator = provider.GetRequiredService<IMediator>();
        var requests = new[]
        {
            new AgreementCategoryTermResolutionRequest(SupplierId, 2, (short)SalesChannel.Online, At),
            new AgreementCategoryTermResolutionRequest(SupplierId, 3, (short)SalesChannel.Online, At)
        };

        var result = await mediator.Send(new ResolveAgreementCategoryTermsBatchQuery(requests));

        Assert.Equal([2, 3], result.Select(x => x.Term!.RequestedCategoryId));
        Assert.Equal(1, repository.CandidateReadCount);
        Assert.Equal(1, categories.CallCount);
    }

    [Fact]
    public async Task Batch_honors_cancellation()
    {
        using var provider = BuildProvider(new FakeAgreementRepository([]), new FakeCategoryTreeHandler());
        var mediator = provider.GetRequiredService<IMediator>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mediator.Send(
            new ResolveAgreementCategoryTermsBatchQuery([
                new(SupplierId, 3, (short)SalesChannel.Online, At)
            ]), cts.Token));
    }

    private static async Task<ResolvedAgreementCategoryTermDto?> ResolveAsync(
        IReadOnlyList<AgreementCategoryTermCandidate> candidates,
        short channel = (short)SalesChannel.Online,
        DateTimeOffset? at = null)
    {
        using var provider = BuildProvider(new FakeAgreementRepository(candidates), new FakeCategoryTreeHandler());
        return await provider.GetRequiredService<IMediator>().Send(
            new ResolveAgreementCategoryTermQuery(SupplierId, 3, channel, at ?? At));
    }

    private static ServiceProvider BuildProvider(
        FakeAgreementRepository repository,
        FakeCategoryTreeHandler categories)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(typeof(ResolveAgreementCategoryTermsBatchQueryHandler).Assembly);
        services.AddSingleton<IAgreementRepository>(repository);
        services.AddSingleton<IRequestHandler<GetCategoriesQuery, List<CategoryDto>>>(categories);
        return services.BuildServiceProvider();
    }

    private static AgreementCategoryTermCandidate Candidate(int categoryId, decimal commission) => new(
        Guid.NewGuid(), Guid.NewGuid(), SupplierId, categoryId,
        SalesChannel.Online, commission, false, At.AddDays(-1),
        AgreementStatus.Approved, false, At.AddDays(-10), At.AddDays(10));

    private sealed class FakeCategoryTreeHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
    {
        public int CallCount { get; private set; }

        public Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<List<CategoryDto>>([
                new(1, "ریشه", "root", "store", null, null, 0, true,
                [
                    new(2, "والد", "parent", "store.parent", null, 1, 0, true,
                    [
                        new(3, "فرزند", "child", "store.parent.child", null, 2, 0, true)
                    ])
                ])
            ]);
        }
    }

    private sealed class FakeAgreementRepository(IReadOnlyList<AgreementCategoryTermCandidate> candidates)
        : IAgreementRepository
    {
        public int CandidateReadCount { get; private set; }

        public Task<IReadOnlyList<AgreementCategoryTermCandidate>> GetCategoryTermCandidatesAsync(
            IReadOnlyCollection<Guid> supplierIds, IReadOnlyCollection<int> categoryIds, CancellationToken ct)
        {
            CandidateReadCount++;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(candidates);
        }

        public Task<Agreement?> GetByIdAsync(Guid id, bool includeProducts, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreementProduct?> GetProductByIdAsync(Guid productId, CancellationToken ct) => throw new NotSupportedException();
        public Task<(IReadOnlyList<Agreement> Items, int Total)> GetPagedAsync(Guid? supplierId, AgreementStatus? status, AgreementType? type, string? search, int page, int size, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsByAgreementNoAsync(string agreementNo, Guid? excludeId, CancellationToken ct) => throw new NotSupportedException();
#pragma warning disable CS0618
        public Task<IReadOnlyList<Guid>> GetApprovedProductIdsByCategoryAsync(int categoryId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetDisplayableProductIdsByCategoriesAsync(IReadOnlyList<int> categoryIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, AgreementProductDto>> GetProductsByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct) => throw new NotSupportedException();
#pragma warning restore CS0618
        public Task<IReadOnlyDictionary<Guid, decimal>> GetCommissionPercentsByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task AddAsync(Agreement agreement, CancellationToken ct) => throw new NotSupportedException();
        public void Update(Agreement agreement) => throw new NotSupportedException();
        public void AddProduct(AgreementProduct product) => throw new NotSupportedException();
        public void AddCategoryTerm(AgreementCategoryTerm term) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
