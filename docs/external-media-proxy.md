# External media service

## Ownership and consumer contract
Media owns external URL registration, administrative labels, downloading, storage, status, sliding expiration and eviction. Feature modules only call `IExternalMediaService.GetPublicUrlAsync(url, origin, ct)` or its batch variant and use the returned URL. Missing service/invalid input produces no external URL fallback. Provider adapters remain unchanged.

`origin` contains optional Module and Provider labels used solely by administrative operations. Neither field participates in deduplication, destination permission checks, filenames, TTL, transport, or response URLs. No provider-specific media tables, resolvers or cache configurations exist. Hotels references only Media.Application.Contracts.

`media.external_sources` owns Id, SourceUrl, FileName (SHA-256 of the normalized full URL), CreatedAt, LastAccessedAt, ExpiresAt, RefreshAfter, StateChangedAt and Status. Status is NotCached (0), Cached (1), or Failed (2). Initial LastAccessedAt, ExpiresAt and RefreshAfter are null; generating a URL does not fetch a file or extend its cache retention.

`media.external_source_origins` stores SourceId, Module and Provider with a composite unique key. A single URL can have multiple administrative labels without duplicating its bytes or losing its association with another module. Deleting the cached bytes of such a file affects all its consumers; its persistent URL remains valid and the next GET can download it again.

The previous unapplied HotelsExternalMedia migration was removed with the user's confirmation that it had never been applied. The new `20261020201144_MediaExternalSources` migration creates only Media-owned tables. Deploy it before the updated hotel APIs. Uploaded MediaAsset records and files are unaffected.

## Delivery and retention
`GET|HEAD /media/external/{id:guid}` is binary HTTP content, not a page (no render mode). It bypasses API JSON buffering. Errors use ApiResponseHelper. HEAD never downloads missing content. Range is ignored.

Files use `<SHA256(normalized-url)>.bin` and a small adjacent metadata file. Query strings, including signed tokens, remain part of the source URL and hash; host/scheme normalization does not strip or reorder query parameters. The browser sees only an opaque ID, never the URL, hash filename, module, or provider.

Every successful cached request sets ExpiresAt to **last access + 5 hours**, not previous expiry + 5 hours. Local cache reads update memory only. Access/status observations are batched to PostgreSQL every 5 minutes and on graceful shutdown. Cache access timestamps are also checkpointed by the cleanup worker. An abrupt termination may lose the most recent checkpoint interval. The bounded activity buffer wakes its writer early under pressure; excess observations are coalesced/dropped with a summary log rather than creating unbounded memory use or synchronous per-hit SQL.

Freshness is independent: RefreshAfter defaults to 24 hours after a completed download. Repeated requests extend retention but do not postpone checking the origin for updated content. A request after RefreshAfter fetches a new copy even for a hot image. Eviction for expiry, capacity, or administration removes only disposable cache files; source metadata/IDs remain. Status timestamps are eventually consistent within the flush interval and are not used as a transport gate.

## Configuration and resources
`ExternalMedia:PublicBaseUrl` is the absolute public HTTPS base routing to the API endpoint; never derive it from Host. `CachePath` is a dedicated absolute directory outside MediaStorage:BasePath, owned by one API process. Linux deployments must override the checked-in Windows path. The owner lock prevents sharing the directory between instances.

Defaults: MaxBytes=1073741824; MinFreeBytes=2147483648; MaxImageBytes=5242880; ConcurrentDownloads=4; QueueLimit=32; MaxEntries=20000; SlidingExpirationHours=5; ActivityFlushMinutes=5; FreshHours=24; CleanupMinutes=5; QueueWaitSeconds=2; ConnectSeconds=3; DownloadSeconds=15; NegativeSeconds=30. Browser freshness is at most one hour. Disk entries budget conservatively includes 4096 metadata bytes per file. Configuration changes require restart.

CacheEnabled=false retains bounded proxy streaming without disk writes. A URL is still a Refahi URL. Storage capacity takes precedence over sliding retention; least recently used files can be evicted before ExpiresAt. In-flight file readers are not deleted; an administrative purge marks them for removal after their last reader closes and prevents new hits to that copy.

AllowedHosts is a **flat list of exact hostnames**, selected solely by the source URL. Example:

```json
"AllowedHosts": ["store.snapptrip.com"]
```

Remove the previous provider-keyed AllowedHosts settings, including environment variables such as `ExternalMedia__AllowedHosts__SnappTrip__0`. Use `ExternalMedia__AllowedHosts__0`, `...__1`, etc. Approve the actual image hosts from real supplier responses before adding them; the supplier API host is not necessarily an image host. Administrative provider labels never grant access. Every redirect is checked against the same host allowlist and public-IP restrictions. No wildcards, browser credentials or upstream redirect headers are passed through.

## Administrative cache purge
Admin-only endpoint: `DELETE /api/media/external-cache?module=Hotels&provider=SnappTrip`.
Either filter can be omitted; at least one is required. Providing both means intersection. Module/provider matches are exact stored labels. Authorization requires AdminOnly; validation messages are Persian. The response reports MatchedMedia (source records matched), including records whose bytes were already absent. This is not an inventory-record deletion, permanent provider block, or removal of uploaded files. Do not log source URLs/query strings.

## Operations and verification
Observe meter Refahi.ExternalMedia (hit/miss/eviction/failure, cache_bytes, cache_entries, reserved_bytes, waiting) and activity flush failure/pressure logs. Monitor PostgreSQL mapping/origin row counts separately from the disposable disk budget.
To clear all local bytes manually, stop the owning API, verify the configured absolute cache directory, clear only that directory and restart. Never clear MediaStorage:BasePath or delete mapping tables. Roll back cache writes with CacheEnabled=false; never fall back to exposing supplier URLs.

Tests cover concurrent source registration on disposable PostgreSQL, cross-module/provider deduplication, administrative origin associations, batched access/status writes, five-hour sliding expiration, independent freshness, stable URLs after expiration/purge, hashed filenames, concurrent download sharing, restart, disabled caching, MIME signatures and non-public IP rejection. Set REFAHI_MEDIA_TEST_CONNECTION to a disposable database whose name begins media_cache_test to run the PostgreSQL test. No production database is used by that test.

Before production, separately verify DNS/TLS routing, approved real image hosts, disk permissions and pressure, and browser Network requests in staging. No production activation is performed by local builds/tests.
