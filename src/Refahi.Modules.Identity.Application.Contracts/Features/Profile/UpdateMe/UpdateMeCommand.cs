using System;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Contracts.Features.Profile.UpdateMe;

public record UpdateMeCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Email) : IRequest<UpdateMeResult>;

public record UpdateMeResult(
    bool Success,
    string? ErrorMessage,
    MeDetailDto? Me);
