using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Reviews;

public sealed record CreateReviewCommand(
    Guid ProductId, Guid UserId, int Rating, string? Comment
) : IRequest<CreateReviewResponse>;

public sealed record CreateReviewResponse(Guid ReviewId);
