using MediatR;
using Refahi.Modules.Media.Application.Contracts.Queries;
using Refahi.Modules.Media.Application.Services;

namespace Refahi.Modules.Media.Application.Features.ResolveUrls;

public sealed class ResolveMediaPublicUrlsQueryHandler
    : IRequestHandler<ResolveMediaPublicUrlsQuery, IReadOnlyList<ResolvedMediaPublicUrl>>
{
    private readonly IMediaStorageService _storage;

    public ResolveMediaPublicUrlsQueryHandler(IMediaStorageService storage)
        => _storage = storage;

    public async Task<IReadOnlyList<ResolvedMediaPublicUrl>> Handle(
        ResolveMediaPublicUrlsQuery request,
        CancellationToken cancellationToken
    )
    {
        return await Task.WhenAll(
            request.CandidateSets.Select(candidateSet =>
                ResolveCandidateSetAsync(candidateSet, cancellationToken)
            )
        );
    }

    private async Task<ResolvedMediaPublicUrl> ResolveCandidateSetAsync(
        MediaPublicUrlCandidateSet candidateSet,
        CancellationToken cancellationToken
    )
    {
        foreach (var storagePath in candidateSet.StoragePaths)
        {
            var publicUrl = await _storage.ResolvePublicUrlAsync(
                storagePath,
                cancellationToken
            );
            if (publicUrl is not null)
                return new ResolvedMediaPublicUrl(candidateSet.Key, publicUrl);
        }

        return new ResolvedMediaPublicUrl(candidateSet.Key, null);
    }
}
