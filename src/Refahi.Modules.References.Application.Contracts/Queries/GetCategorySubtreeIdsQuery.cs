using MediatR;

namespace Refahi.Modules.References.Application.Contracts.Queries;

/// <summary>
/// Returns the ID of the root category plus all active descendant CategoryIds (transitive).
/// Returns an empty list if the root does not exist or is inactive.
/// </summary>
public sealed record GetCategorySubtreeIdsQuery(int RootCategoryId)
    : IRequest<IReadOnlyList<int>>;
