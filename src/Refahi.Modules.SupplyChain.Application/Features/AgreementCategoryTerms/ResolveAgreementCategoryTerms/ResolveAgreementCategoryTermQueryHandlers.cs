using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementCategoryTerms;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementCategoryTerms.ResolveAgreementCategoryTerms;

public sealed class ResolveAgreementCategoryTermQueryHandler
    : IRequestHandler<ResolveAgreementCategoryTermQuery, ResolvedAgreementCategoryTermDto?>
{
    private readonly IMediator _mediator;

    public ResolveAgreementCategoryTermQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<ResolvedAgreementCategoryTermDto?> Handle(
        ResolveAgreementCategoryTermQuery request,
        CancellationToken cancellationToken
    )
    {
        var batch = await _mediator.Send(
            new ResolveAgreementCategoryTermsBatchQuery([
                new AgreementCategoryTermResolutionRequest(
                    request.SupplierId,
                    request.CategoryId,
                    request.SalesChannel,
                    request.AtUtc
                ),
            ]),
            cancellationToken
        );
        return batch[0].Term;
    }
}

public sealed class ResolveAgreementCategoryTermsBatchQueryHandler
    : IRequestHandler<
        ResolveAgreementCategoryTermsBatchQuery,
        IReadOnlyList<AgreementCategoryTermBatchResult>
    >
{
    private readonly IAgreementRepository _repository;
    private readonly IMediator _mediator;
    private readonly ILogger<ResolveAgreementCategoryTermsBatchQueryHandler> _logger;

    public ResolveAgreementCategoryTermsBatchQueryHandler(
        IAgreementRepository repository,
        IMediator mediator,
        ILogger<ResolveAgreementCategoryTermsBatchQueryHandler> logger
    ) => (_repository, _mediator, _logger) = (repository, mediator, logger);

    public async Task<IReadOnlyList<AgreementCategoryTermBatchResult>> Handle(
        ResolveAgreementCategoryTermsBatchQuery request,
        CancellationToken cancellationToken
    )
    {
        if (request.Requests.Count == 0)
            return [];

        var stopwatch = Stopwatch.StartNew();
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CategoryDto> tree;
        try
        {
            tree = await _mediator.Send(
                new GetCategoriesQuery(IncludeInactive: false),
                cancellationToken
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Agreement category term hierarchy resolution failed for {RequestCount} requests",
                request.Requests.Count
            );
            throw;
        }
        var hierarchy = CategoryHierarchy.Create(tree);
        var requestedCategoryIds = request.Requests.Select(x => x.CategoryId).Distinct().ToArray();
        var ancestorIds = requestedCategoryIds
            .SelectMany(hierarchy.GetAncestorIds)
            .Distinct()
            .ToArray();

        // A missing/inactive requested category intentionally resolves to no term (fail closed).
        var candidates = await _repository.GetCategoryTermCandidatesAsync(
            request.Requests.Select(x => x.SupplierId).Distinct().ToArray(),
            ancestorIds,
            cancellationToken
        );

        var results = new List<AgreementCategoryTermBatchResult>(request.Requests.Count);
        foreach (var item in request.Requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ancestors = hierarchy.GetAncestorIds(item.CategoryId).ToHashSet();
            var requestedChannel = (SalesChannel)item.SalesChannel;
            var atUtc = item.AtUtc.ToUniversalTime();

            var winner = candidates
                .Where(x =>
                    x.SupplierId == item.SupplierId
                    && ancestors.Contains(x.CategoryId)
                    && !x.TermIsDeleted
                    && !x.AgreementIsDeleted
                    && x.AgreementStatus == AgreementStatus.Approved
                    && x.AgreementFromDate <= atUtc
                    && atUtc < x.AgreementToDate
                    && (x.AllowedSalesChannels & requestedChannel) == requestedChannel
                )
                .OrderByDescending(x => hierarchy.GetDepth(x.CategoryId))
                .ThenByDescending(x => x.AgreementFromDate)
                .ThenByDescending(x => x.TermCreatedAt)
                .ThenBy(x => x.TermId)
                .FirstOrDefault();

            var resolved = winner is null
                ? null
                : new ResolvedAgreementCategoryTermDto(
                    winner.TermId,
                    winner.AgreementId,
                    winner.SupplierId,
                    winner.CategoryId,
                    item.CategoryId,
                    (short)winner.AllowedSalesChannels,
                    winner.CommissionPercent,
                    winner.AgreementFromDate,
                    winner.AgreementToDate
                );
            results.Add(new AgreementCategoryTermBatchResult(item, resolved));
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Agreement category term batch resolved: Requests={RequestCount}, Hits={HitCount}, Misses={MissCount}, ElapsedMs={ElapsedMs}",
            results.Count,
            results.Count(x => x.Term is not null),
            results.Count(x => x.Term is null),
            stopwatch.Elapsed.TotalMilliseconds
        );
        return results;
    }

    private sealed class CategoryHierarchy
    {
        private readonly Dictionary<int, int?> _parents;
        private readonly Dictionary<int, int> _depths;

        private CategoryHierarchy(Dictionary<int, int?> parents, Dictionary<int, int> depths) =>
            (_parents, _depths) = (parents, depths);

        public static CategoryHierarchy Create(IReadOnlyList<CategoryDto> roots)
        {
            var parents = new Dictionary<int, int?>();
            var depths = new Dictionary<int, int>();

            void Visit(CategoryDto category, int depth)
            {
                parents[category.Id] = category.ParentId;
                depths[category.Id] = depth;
                if (category.Children is null)
                    return;
                foreach (var child in category.Children)
                    Visit(child, depth + 1);
            }

            foreach (var root in roots)
                Visit(root, 0);
            return new CategoryHierarchy(parents, depths);
        }

        public IReadOnlyList<int> GetAncestorIds(int categoryId)
        {
            if (!_parents.ContainsKey(categoryId))
                return [];
            var result = new List<int>();
            int? current = categoryId;
            while (current.HasValue && _parents.TryGetValue(current.Value, out var parent))
            {
                result.Add(current.Value);
                current = parent;
            }
            return result;
        }

        public int GetDepth(int categoryId) =>
            _depths.TryGetValue(categoryId, out var depth) ? depth : -1;
    }
}
