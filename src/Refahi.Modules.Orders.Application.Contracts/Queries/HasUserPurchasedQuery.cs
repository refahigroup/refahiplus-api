using MediatR;
using System;

namespace Refahi.Modules.Orders.Application.Contracts.Queries;

/// <summary>
/// Checks if a given user has at least one Delivered order from the specified source.
/// </summary>
public sealed record HasUserPurchasedQuery(
    Guid UserId,
    string SourceModule,
    Guid SourceReferenceId
) : IRequest<bool>;
