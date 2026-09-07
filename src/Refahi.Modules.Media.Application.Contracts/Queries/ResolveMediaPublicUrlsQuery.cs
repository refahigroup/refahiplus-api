using MediatR;

namespace Refahi.Modules.Media.Application.Contracts.Queries;

public sealed record MediaPublicUrlCandidateSet(
    string Key,
    IReadOnlyList<string> StoragePaths
);

public sealed record ResolvedMediaPublicUrl(string Key, string? PublicUrl);

public sealed record ResolveMediaPublicUrlsQuery(
    IReadOnlyList<MediaPublicUrlCandidateSet> CandidateSets
) : IRequest<IReadOnlyList<ResolvedMediaPublicUrl>>;
