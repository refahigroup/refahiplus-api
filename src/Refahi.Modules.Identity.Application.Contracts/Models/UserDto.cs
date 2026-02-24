using System;
using System.Collections.Generic;

namespace Refahi.Modules.Identity.Application.Contracts.Models;

public record UserDto(
    Guid Id,
    string? MobileNumber,
    string? Email,
    bool IsActive,
    IReadOnlyList<string> Roles);

