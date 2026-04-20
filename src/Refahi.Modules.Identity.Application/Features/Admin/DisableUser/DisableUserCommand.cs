using System;
using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Admin.DisableUser;

public record DisableUserCommand(Guid UserId) : IRequest<DisableUserResult>;

public record DisableUserResult(bool Success, string? ErrorMessage = null);
