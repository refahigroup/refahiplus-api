# Local hotel content — implementation and rollout

## Behavior and boundaries

Hotels owns static supplier content in the existing provider-prefixed discovery tables. Nullable `ContentJson` (jsonb), `AccommodationType`, `Rating`, `FacilityKeys` (text[]) and `ContentReady` extend each hotel row. City refresh states have independent nullable `NextContentAttemptAtUtc` and `LastContentSuccessAtUtc`. The normalized content document is the current provider snapshot: identity, detailed facilities, general policies, static room definitions, gallery metadata/source provenance, public reviews, section timestamps/statuses and a version. Supplier user IDs, moderation scores, credentials, and booking/availability payloads are excluded. Original gallery sources are removed from public profiles; public URLs point to persistent Media files. Reviews are paginated separately rather than embedded unboundedly in every profile/search response.

No transaction tables, historical offer tokens, reservations, orders, mappings or HotelKey values are renamed or rewritten. Canonical geography remains References-owned. Existing `provider_inventory`/`offer_snapshots` ownership extraction remains deferred. Supplier-specific terms on a rate are still verified live during quote/purchase; general cancellation text is informational.

| Provider | Static sources | Explicit limitations |
|---|---|---|
| SnappTrip | City list, batched hotel details (max 10), per-hotel facilities/gallery/rooms/reviews | Review publication status allowlist must be confirmed before enabling; unknown statuses are never published. Missing coordinates remain unknown. |
| Lamasoo | Hotel inventory, including static rooms and child policies when supplied | The current contract supplies no verified detailed facilities, public reviews or GIS. Unsupported sections are marked explicitly; price, room counts remaining and daily restrictions are excluded from static content. |

The worker runs on startup and scans every minute when enabled. Each section succeeds for 24 hours and retries after 15 minutes; each supplier request has a 30-second deadline. Calls are sequential (one in flight), and advisory locks coordinate multiple hosts. The old discovery worker skips scopes handled by the new worker. Core detail/inventory payloads are reused within the sweep. HTTP and Media writes are outside hotel DB transactions; each hotel's resulting content is saved atomically. Completed sections survive restart and are not fetched again before their deadline. Failed sections retain their last successful data and success timestamp. Only a complete validated city inventory authorizes retirement; partial/error responses do not.

Activated scopes keep last-success content without a 7-day cutoff and stay visible when provider search/purchase is disabled. Freshness is still reported after 24 hours. Explicit retirement/inactive canonical geography remains effective. Request-time local reads never fall back to static supplier APIs. Outside activated scopes the former read path remains available for rollback.

## Configuration (all new functionality defaults OFF)

```json
{
  "Hotels": {
    "StaticContent": {
      "WorkerEnabled": false,
      "ReadEnabled": false,
      "SyncScopes": ["SnappTrip:1", "Lamasoo:1"],
      "ReadScopes": [],
      "RefreshHours": 24,
      "RetryMinutes": 15,
      "RequestTimeoutSeconds": 30,
      "SnappTripPublishedReviewStatuses": []
    }
  },
  "PersistentExternalMedia": {
    "Enabled": false,
    "StoragePath": "/var/lib/refahiplus/hotel-images",
    "MinFreeBytes": 2147483648
  }
}
```

Scope numbers are **canonical reference city IDs**, not supplier city IDs. Empty SyncScopes means all mapped active cities; use an explicit pilot list initially. ReadEnabled requires a nonempty explicit ReadScopes list. Do not copy the example city IDs without checking deployment geography. Do not guess review statuses: verify actual contract-approved published values, then populate the allowlist. An empty allowlist marks SnappTrip reviews failed and prevents readiness instead of claiming there are no reviews.

Persistent Media uses existing source-host allowlisting, SSRF protections, byte/pixel limits and image optimization. Files are WebP in a separate durable directory, keyed by a deterministic source identity. Files have per-file process locks and atomic replacement. Download/refresh happens only in background synchronization. Failure, cancellation, insufficient storage and transient-cache eviction cannot delete a last-good persistent file. There is no automatic persistent-file deletion in this release. Existing transient-media behavior for other modules is unchanged. Provision storage, backups and read/write permissions on every serving host; mount the same persistent directory across hosts. Budget capacity using actual accepted images, source limits and pilot measurements; the free-space reserve is not an overall storage quota.

## Interfaces

- `GET /api/hotels/v2/{hotelKey}/profile`: compatible existing fields plus optional structured Content, IsLocalContent and provenance identity. In activated scopes, Images are persistent URLs; no Media registration/download takes place during the read.
- `GET /api/hotels/v2/legacy/{hotelId}/profile`: local SnappTrip numeric-identity lookup; 404 outside activated scopes or for an unpublished profile.
- `GET /api/hotels/v2/search`: date-free search. Parameters: q, referenceCityId, provinceId, repeated stars/accommodations/facilities, minRating, latitude/longitude, sort=title|rating|distance, page (1–10000), pageSize (default 20, max 50). Distance sort requires coordinates. Each query word and facility must match; missing ratings do not satisfy a minimum. Stable provider/title/identity ordering breaks ties. Count/selection/hydration share a repeatable-read transaction.
- `GET /api/hotels/v2/{hotelKey}/reviews`: page default 1, pageSize default 20/max 50. Published local reviews ordered by registered date descending then stable ID; section status distinguishes unavailable from empty.
- `GET /api/hotels/v2/{hotelKey}/availability`: existing date/party parameters and offer semantics, without requesting static hotel details. Activated SnappTrip room validation uses local definitions; Lamasoo presentation inventory also comes from local rows. Actual availability/quote/purchase remains provider-bound and live as before.
- `GET /api/hotels/v2/content/readiness`: AdminOnly; per-provider/reference-city counts of published hotels, ready content and missing persistent files. Historical rows without content fail readiness. An empty result is not readiness certification.
- `GET|HEAD /media/persistent/{id}`: local file only; missing files return 404 without supplier fallback.

The Web `/trip/hotels/explore` route is SSR with GET filters/pagination. Both detail identities remain supported. Activated detail pages render all static content and reviews on the server; only date-dependent availability is an independent WASM island with prerender=false. Failed availability leaves the static page available. Static room cards cannot initiate a booking without an actual valid offer. API calls remain network-only under the existing service worker.

## Safe deployment and rollback

1. Record the currently deployed revisions/configuration and response/error/latency/booking baselines. Preserve the pre-existing uncommitted Catalogue/discovery work when integrating this change. Local baseline before this task: Hotels 133 passing, one PostgreSQL fixture skipped until a disposable connection was supplied.
2. Apply the additive `HotelsCompleteStaticContent` migration in a separate deployment step **before** new application instances serve traffic. It uses a five-second PostgreSQL lock timeout and nullable additions. The existing application startup migration hook still exists; pre-application deployment prevents it from doing first-time DDL on a traffic-serving startup. Do not use Down migrations operationally.
3. Deploy compatible backend with the new flags OFF. Previous binaries remain compatible with the additive schema. Do not change existing Catalogue, search, purchase or auth flags as part of this rollout.
4. Provision/verify persistent storage and allowed supplier image hosts; enable persistent Media. Confirm review publication statuses with the supplier. Enable only the worker for explicit pilot SyncScopes; leave ReadEnabled=false.
5. Check readiness against the complete expected mapped inventory. Every published hotel in the pilot must be represented, every supported section successful, and every accepted gallery file locally readable. Unsupported sections must match the reviewed capability table. Resolve all failed sections or missing files. Recheck after a new worker success; do not infer readiness from empty counts or merely having a profile row.
6. In staging, compare local content with supplier fixtures/approved samples and block supplier/image-source traffic. Verify text, filters, images and reviews survive. Measure representative p95 against the previous implementation in the same environment; >10% regression or increased booking failures blocks rollout. No shadow supplier calls are added to production requests.
7. Enable ReadEnabled and the pilot ReadScopes, then deploy compatible Web. Observe at least 24 hours before expanding. Monitor publication/section counts and times, missing media, disk space, 4xx/5xx rates, p95, and booking/confirmation/refund success. Stop expansion on regression. New scopes require the same coverage/readiness checks.
8. Roll back read behavior by disabling ReadEnabled; stop new synchronization separately with WorkerEnabled=false if necessary. Keep the additive columns, content and persistent files. Keep persistent serving enabled while already rendered pages can reference those URLs. Previous Web/API versions remain deployable; no historic data rollback is needed.

Structured logs include provider, reference city, section, count/readiness and exception type; no raw URLs, guest information, review text or supplier responses are logged. Meter `Refahi.Hotels.StaticContent` exposes `hotel_content.sections` and `hotel_content.duration` by provider/section/status. Readiness supplies coverage and missing-file diagnostics. Storage free-space/backup/host checks and production p95/booking baselines are deployment gates, not claims inferred from local unit tests.

## Verification record

Automated checks cover static-only supplier calls, preserved successful sections on review/media failures, unverified status rejection, Lamasoo rate exclusion, startup defaults, nullable migration model and SQL translation; persistent media restart/offline reads, concurrency, failed refresh, disk exhaustion and host restrictions; historical PostgreSQL upgrade, stale content, supplier-disabled discovery, filters, ordering and review pagination; SSR detail content and availability failure isolation. The final task report records exact test counts. Live supplier payload certification, full-inventory completion, production storage/backup checks, 24-hour pilot observation and production-scale p95 must be completed during rollout.

Local verification on 2026-10-23: both complete solutions build in Release. Tests passed: 142 backend Hotels (including the disposable historical PostgreSQL upgrade and local-content SQL scenarios), 51 Media (including disposable PostgreSQL), 21 Catalogue, 17 Orders, 10 Wallets; 31 frontend Hotels, 12 Catalogue, 5 Checkout, 25 Shell; 9 service-worker checks. Browser QA in headless Edge verifies GUID/numeric profiles with JavaScript disabled, local images/static rooms/rules/reviews, date-free SSR search and URL pagination/Back, review pagination, exactly one availability request isolated from static rendering, no JavaScript runtime errors and no mobile horizontal overflow. Browser data is synthetic; PostgreSQL runs on a dedicated temporary loopback cluster with ICU locale, not application configuration. Existing dependency vulnerability and nullable warnings remain outside this change.

QA cleanup: the fixture Web/Python services and temporary PostgreSQL server were stopped. Automatic command policy rejected recursive removal of `C:/Users/Saeed/AppData/Local/Temp/refahi-hotels-static-postgres`; its binaries, archive and disposable test databases remain there, with no running database service. Production data/configuration was not modified.
