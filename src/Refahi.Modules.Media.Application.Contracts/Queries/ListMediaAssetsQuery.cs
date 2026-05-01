using MediatR;
using Refahi.Modules.Media.Application.Contracts.DTOs;

namespace Refahi.Modules.Media.Application.Contracts.Queries;

public sealed record ListMediaAssetsQuery(
    int Page = 1,
    int Size = 20,
    string? EntityType = null,
    Guid? UploadedBy = null
) : IRequest<ListMediaAssetsResult>;

public sealed record ListMediaAssetsResult(
    IReadOnlyList<MediaAssetDto> Items,
    int Total,
    int Page,
    int Size
);
