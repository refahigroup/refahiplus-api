using System;
using System.Collections.Generic;
using Refahi.Modules.Identity.Application.Contracts.Models;

namespace Refahi.Modules.Identity.Application.Contracts.Models;

public record MeDetailDto(
    Guid Id,
    string? MobileNumber,
    string? Email,
    bool IsActive,
    IReadOnlyList<string> Roles,
    UserProfileDto? Profile);
