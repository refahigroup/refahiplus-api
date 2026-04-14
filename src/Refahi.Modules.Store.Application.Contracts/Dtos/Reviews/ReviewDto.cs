namespace Refahi.Modules.Store.Application.Contracts.Dtos.Reviews;

public sealed record ReviewDto(
    Guid Id, Guid UserId, int Rating, string? Comment, DateTimeOffset CreatedAt);
