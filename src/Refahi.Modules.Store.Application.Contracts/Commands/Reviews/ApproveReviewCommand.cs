using MediatR;
using System;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Reviews;

public sealed record ApproveReviewCommand(Guid ReviewId) : IRequest<ApproveReviewResponse>;
public sealed record ApproveReviewResponse(Guid ReviewId, bool IsApproved);
