using System;
using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Admin.EnableUser;

public record EnableUserCommand(Guid UserId) : IRequest<EnableUserResult>;

public record EnableUserResult(bool Success, string? ErrorMessage = null);
