namespace Refahi.Modules.Store.Application.Contracts.Dtos.Modules;

public sealed record ModuleDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string? IconUrl,
    bool IsActive,
    int SortOrder);
