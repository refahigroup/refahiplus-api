using System;
using MediatR;

namespace Refahi.Modules.Identity.Application.Features.Admin.EditUser;

public record AdminEditUserCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string? NationalCode = null) : IRequest<AdminEditUserResult>;

public record AdminEditUserResult(bool Success, string? ErrorMessage = null);
