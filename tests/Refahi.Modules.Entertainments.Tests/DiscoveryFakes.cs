using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Media.Application.Contracts.External;

namespace Refahi.Modules.Entertainments.Tests;

internal sealed class FakeMedia : IPersistentExternalMedia
{
    public Task<Guid> EnsureAsync(string url, ExternalMediaOrigin origin, CancellationToken ct) => Task.FromResult(Guid.NewGuid());
    public string GetUrl(Guid id) => $"/api/media/persistent/{id}";
    public bool Exists(Guid id) => true;
}
internal sealed class FakeDiscovery(string nativeId) : IEntertainmentDiscovery
{
    public Task<string?> ResolveNativeAsync(string key, CancellationToken ct) => Task.FromResult<string?>(nativeId);
    public Task<Guid?> ResolveProgramKeyAsync(string id, CancellationToken ct) => Task.FromResult<Guid?>(Guid.Parse(id));
    public Task<EntertainmentProgramDto?> ProfileAsync(Guid key, CancellationToken ct) => Task.FromResult<EntertainmentProgramDto?>(null);
    public Task<EntertainmentDiscoveryPage> SearchAsync(EntertainmentDiscoverySearch query, int take, CancellationToken ct) => Task.FromResult(new EntertainmentDiscoveryPage([], 0, false));
    public Task<IReadOnlyList<EntertainmentDiscoveryCity>> CitiesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<EntertainmentDiscoveryCity>>([]);
    public Task<IReadOnlyList<EntertainmentLocationMappingDto>> MappingsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<EntertainmentLocationMappingDto>>([]);
    public Task SetMappingAsync(string code, int city, Guid actor, CancellationToken ct) => Task.CompletedTask;
}
