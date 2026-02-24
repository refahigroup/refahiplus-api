using System;
using Refahi.Modules.Identity.Domain.ValueObjects;

namespace Refahi.Modules.Identity.Application.Contracts.Models;

public record UserProfileDto(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string? NationalCode,
    Gender? Gender);
