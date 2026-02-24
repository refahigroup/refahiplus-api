using Refahi.Modules.Identity.Domain.ValueObjects;

namespace Refahi.Modules.Identity.Api.Endpoints.Profile;

public record CreateOrUpdateProfileRequest(
    string FirstName,
    string LastName,
    string? NationalCode,
    Gender? Gender);
