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

Defaults: MaxBytes=1073741824; MinFreeBytes=2147483648; MaxImageBytes=5242880 (output); MaxSourceBytes=20971520 (input); MaxImageDimension=1600; MaxSourcePixels=40000000; ConcurrentDownloads=4; QueueLimit=32; MaxEntries=20000; SlidingExpirationHours=5; ActivityFlushMinutes=5; FreshHours=24; CleanupMinutes=5; QueueWaitSeconds=2; ConnectSeconds=3; DownloadSeconds=15; NegativeSeconds=30. Browser freshness is at most one hour. Disk entries budget conservatively includes 4096 metadata bytes per file. Configuration changes require restart.

CacheEnabled=false retains bounded proxy streaming without disk writes. A URL is still a Refahi URL. Storage capacity takes precedence over sliding retention; least recently used files can be evicted before ExpiresAt. In-flight file readers are not deleted; an administrative purge marks them for removal after their last reader closes and prevents new hits to that copy.

## Heavy-image optimization

Optimization belongs entirely to Media. No provider/module adapter, registration contract, URL mapping, database migration or browser change is required. Module/provider remain administrative labels. Existing public IDs and URL-hash filenames remain stable; the stored bytes, MIME and local ETag describe the resulting representation.

On a cache miss with safe writable storage, reserve `MaxSourceBytes + MaxImageBytes + 8192` bytes before accepting the download (about 25 MiB by default). This reservation covers input, output and metadata, including simultaneous downloads; pending writes also reserve file-count capacity. Free-space checks use the filesystem containing the cache directory, including Linux bind mounts. The bounded input is spooled to `<hash>.bin.input.part`; Content-Length is an early check only and actual reads enforce MaxSourceBytes.

Inputs at or below MaxImageBytes retain their original bytes and detected MIME. Larger inputs are decoded only after validating dimensions (at most 16,384 on either axis, at most MaxSourcePixels total) and frame count. Heavy animated GIF/WebP files are rejected with AnimatedImageTooLarge rather than silently losing animation. Single-frame JPEG, PNG, WebP and GIF can be optimized. Preserve orientation and transparency, fit within MaxImageDimension × MaxImageDimension without cropping/upscaling, and encode WebP at qualities 80, 65, 50, then 35 until the output fits MaxImageBytes. Failure to fit rejects the image; no unbounded quality loop is used. Output contains newly encoded pixels without source metadata. Only the final representation is kept in cache; input/output partials are removed after failure, cancellation or completion, and on startup after a crash. If temporary-file deletion fails, retain its entire reservation until restart and log the media ID, avoiding unaccounted disk growth.

One optimizer runs per API instance. Waiting for it is bounded by QueueWaitSeconds within the existing download/request limits; saturation returns 503. Native decode/encode calls cannot be interrupted mid-call: cancellation/deadline is checked between calls, and the task is awaited to completion before releasing its processing slot or scratch reservation. Pixel limits and one processing slot bound the workload, but are not a hard operating-system RSS limit. A full 40-million-pixel RGBA decode alone can require about 160 MB plus codec/output overhead. Leave memory headroom above the API's ordinary usage and reduce MaxSourcePixels for smaller containers. Skia can decode supported formats directly at a reduced resolution; formats without that support may need the full bounded bitmap.

When cache storage is disabled/unwritable or scratch budget/free space is insufficient, no in-memory optimization or second temporary directory is used. Known heavy inputs return 503 for frontend fallback. Unknown-length inputs retain bounded streaming, aborting if they exceed MaxImageBytes. Small images can still stream without caching. The input limit cannot exceed 100 MiB, the pixel limit cannot exceed 40 million, the output dimension cannot exceed 1600, and startup requires enough MaxBytes for one complete scratch reservation. Defaults remain 20 MiB/40 MP/1600.

The processor uses SkiaSharp 3.119.4 and matching SkiaSharp.NativeAssets.Linux.NoDependencies (MIT). The Linux native runtime asset is included transitively in API build/publish; no image-processing binary or apt package is added to the Dockerfile. Keep both package versions aligned.

Production rollout: publish the API image with these dependencies and keep the existing dedicated host bind mount. The three new settings have defaults and are included in base/Development configuration; optional Docker overrides are `ExternalMedia__MaxSourceBytes=20971520`, `ExternalMedia__MaxImageDimension=1600`, `ExternalMedia__MaxSourcePixels=40000000`. Keep MaxImageBytes=5242880 and the 1 GiB budget. Restart the single API container, then GET an existing failed media URL; HEAD does not exercise optimization. Check image/webp, output dimensions/size, subsequent cache hit, and absence of partials. No mapping reset, SQL update or manual cache deletion is needed. Roll back accepting large inputs with MaxSourceBytes=5242880 (equal to MaxImageBytes) and restart; existing optimized cache hits stay valid until normal expiry/eviction.

AllowedHosts is a **flat list of exact hostnames**, selected solely by the source URL. Example:

```json
"AllowedHosts": ["store.snapptrip.com", "s3.ir-thr-at1.arvanstorage.com"]
```

Remove the previous provider-keyed AllowedHosts settings, including environment variables such as `ExternalMedia__AllowedHosts__SnappTrip__0`. Use `ExternalMedia__AllowedHosts__0`, `...__1`, etc. Approve the actual image hosts from real supplier responses before adding them; the supplier API host is not necessarily an image host. Administrative provider labels never grant access. Every redirect is checked against the same host allowlist and public-IP restrictions. No wildcards, browser credentials or upstream redirect headers are passed through.

Actual SnappTrip hotel images also use `s3.ir-thr-at1.arvanstorage.com` (verified against the Tochal hotel source). Preserve that exact host as AllowedHosts index 1 when configuring Production environment variables. `fs.snapptrip.com` returned by some records is a bare HTML homepage, not an image; it is not added to the allowlist.

Image MIME is derived from recognized JPEG/PNG/GIF/WebP signatures, not from the supplier's Content-Type or filename extension. Real supplier records include JPEG bytes labeled application/octet-stream and PNG bytes labeled image/jpeg. The proxy returns the detected canonical image type, or image/webp for optimized images. HTML, empty responses and unrecognized content remain rejected; the final output cap stays 5 MiB. Logs include ImageTooLarge, UnsupportedImageContent, ImagePixelLimit, InvalidImageContent, AnimatedImageTooLarge, OptimizedImageTooLarge, ImageProcessingBusy or ImageProcessingStorageUnavailable without leaking source URLs. Negative-cache hits do not extend the retry deadline.

## Administrative cache purge
Admin-only endpoint: `DELETE /api/media/external-cache?module=Hotels&provider=SnappTrip`.
Either filter can be omitted; at least one is required. Providing both means intersection. Module/provider matches are exact stored labels. Authorization requires AdminOnly; validation messages are Persian. The response reports MatchedMedia (source records matched), including records whose bytes were already absent. This is not an inventory-record deletion, permanent provider block, or removal of uploaded files. Do not log source URLs/query strings.

## Operations and verification
Observe meter Refahi.ExternalMedia (hit/miss/eviction/failure, cache_bytes, cache_entries, reserved_bytes, waiting) and activity flush failure/pressure logs. Monitor PostgreSQL mapping/origin row counts separately from the disposable disk budget.
Optimization adds `external_media.events` kind `optimized` and `external_media.processing` (0 or 1).
To clear all local bytes manually, stop the owning API, verify the configured absolute cache directory, clear only that directory and restart. Never clear MediaStorage:BasePath or delete mapping tables. Roll back cache writes with CacheEnabled=false; never fall back to exposing supplier URLs.

Tests cover concurrent source registration on disposable PostgreSQL, cross-module/provider deduplication, administrative origin associations, batched access/status writes, five-hour sliding expiration, independent freshness, stable URLs after expiration/purge, hashed filenames, concurrent download sharing, restart, disabled caching, MIME signatures and non-public IP rejection. Set REFAHI_MEDIA_TEST_CONNECTION to a disposable database whose name begins media_cache_test to run the PostgreSQL test. No production database is used by that test.
Heavy-image tests additionally cover known/unknown input length, optimization/cache/restart, concurrent requests for the same image, source/output/pixel limits, invalid/animated images, EXIF orientation, transparency, cancellation cleanup, metadata-publication failure and missing scratch capacity. The 16 optimization cases also pass in the .NET Linux SDK container. A live regression using the original 8,793,879-byte SnappTrip cover yielded a 232,600-byte 1600×900 WebP and a cache hit without remaining partial files.

Before production, separately verify DNS/TLS routing, approved real image hosts, disk permissions and pressure, and browser Network requests in staging. No production activation is performed by local builds/tests.
