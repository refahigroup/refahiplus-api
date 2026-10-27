# Catalogue V1

Catalogue federates public discovery queries through registered Application.Contracts sources. Hotels is the only initial source. Each source owns its read model and caching. Catalogue has no product replica; its schema-only migration creates catalogue.

## Ownership and operation

Hotels stores normalized static profiles and per-city refresh states in snapptrip_discovery_* and lamaso_discovery_* tables. Existing HotelKey identifiers and Provider + NativeId uniqueness remain authoritative. No cross-provider merging, price, capacity or reservation happens in discovery. References owns canonical geography. Media owns public image URLs.

The Hotels worker scans active mapped reference cities once per minute, refreshes successful city inventories every 24 hours and retries failures after 15 minutes. Each city publishes atomically only after complete validation. Last successful data survives failure; rows older than 24 hours are marked stale and after 7 days become unavailable. Native inventory fetches happen outside DB transactions. Existing catalogue advisory lock protects identity publication against availability.

## Deployment

Defaults are OFF. Apply the additive migrations first. Set Hotels__Discovery__WorkerEnabled=true, verify successful coverage and counts for both enabled providers, then set Catalogue__Enabled=true on API and Web hosts. Discovery respects Hotels:Providers:*:SearchEnabled; PurchaseEnabled does not control listing. Roll back by disabling the flags; never drop historical listings, offers or reservations.

Defaults: Catalogue source timeout 3s, total timeout 5s, source concurrency 4; configurable under Catalogue. Hotel discovery RefreshHours=24, MaxAgeDays=7, RetryMinutes=15, RequestTimeoutSeconds=30 under Hotels:Discovery. UI page size 20; API max 50; maximum prefix window 1000.

GET /api/catalogue/items accepts q, cityId, sources, sort, page, pageSize plus the location parameters documented below. GET /api/catalogue/filters/cities supplies canonical city filters. GET /api/hotels/v2/{hotelKey}/profile supplies static details without availability. No provider HTTP is used in these request paths; image registration uses Media.

An incomplete coverage/source result has IsPartial=true and no exact TotalCount. All unavailable sources produce 503, while successfully initialized empty inventories produce 200. Source errors expose only safe status, not supplier error payloads.

Observe source latency logs, unavailable/partial responses, per-provider/city publish counts, sync status, LastSuccessAtUtc and NextAttemptAtUtc. Live credentials and payloads must not enter logs. Validate provider contracts and initial sweep duration in staging before enabling UI.

## Deferred ownership extraction

Legacy hotels.provider_inventory and offer_snapshots remain historical existing storage in this change. A separate migration must split rows by provider into provider-prefixed tables, update consumers and preserve offer tokens, recovery and booking identities. Do not rename a multi-provider table wholesale. Existing migration archives retain their historical names.

## Verification

Catalogue tests cover federated prefix pagination, partial/unavailable/empty results, normalization and migration model consistency. Hotels tests cover additive upgrade, identity, provider inventory fixtures, batching, invalid pages and cancellation. PostgreSQL fixtures require an explicitly disposable hotel_city_test database. Browser tests and live provider smoke checks are release gates; runtime metrics are not inferred from unit-test timing.

## Architecture decision record

Status: accepted for V1 (2026-10-22). Choose on-request federation through ICatalogueSource and source-owned read models. A central search index, live fan-out to supplier inventory APIs, a CMS and distributed deep cursors are deferred. Bounded prefix aggregation is exact within the 1000-result window for the shared title-asc ordering. Module-specific ordering and eligibility cannot be reimplemented in Catalogue.

Verified locally: schema-only Catalogue migration applied to disposable PostgreSQL; Hotels historical upgrade fixture passed with additive discovery tables; initial Web HTML contains hotel cards and the date-free profile using a local fixture API. Live supplier contract validation, initial sweep duration and production load measurement remain deployment gates.


## Location and text search decision — 2026-10-23

The only production source remains Hotels. Pools and multi-venue cinema scenarios are contract test fixtures. Each item can expose zero or more source-eligible locations, a display category and plain-text attributes. Catalogue chooses DisplayLocation and recalculates DistanceMeters; legacy city fields mirror that location. Sources must apply text/geography eligibility and return the ordered prefix using CatalogueOrdering before Catalogue performs the global page slice. A movie is one item even with several eligible venues.

`GET /api/catalogue/items` additionally accepts location=all|province|city|nearby, provinceId, latitude and longitude. Missing location infers city, then province, then all. Nearby requires a finite valid origin and forbids administrative filters; it defaults to distance-asc, has no radius, and places unknown GIS last. Other modes use title-asc. Explicit incompatible sorts return 400. Canonical active province/city and their relationship are validated through References contracts. An active location with no source coverage returns an empty successful result. Existing q/cityId/page links and the city filter endpoint remain supported. `GET /api/catalogue/filters/locations` returns covered provinces/cities plus source status and partialness, independently of q.

Haversine uses a 6,371,000-metre radius. Ranking compares unrounded distances, then UTF-8 normalized title, source and stable item ID. Missing GIS alone does not imply partial coverage. Multiple venues use the nearest valid point, or stable location ID when no origin exists. Modules decide which venues are eligible.

The Hotels additive `HotelsDiscoveryLocations` migration adds nullable Latitude/Longitude to both provider-prefixed discovery tables and makes existing city sync states due now without altering last-success/observation timestamps. SnappTrip supplies optional location.lat/lon; malformed or missing coordinates and its default (0,0) become null without discarding the hotel. Lamasoo currently supplies no coordinates. Existing HotelKey and transactional identities are preserved.

Hotels builds SearchText from plain-text names, category, current city/province, address, the same 200-character card summary, provider and known grade. All normalized query words must match. Nearby scans all matched lightweight projections inside Hotels, keeps at most the requested prefix in memory and hydrates only selected items. Candidate count/selection/hydration share a Repeatable Read transaction; Media executes afterward. Non-nearby reads retain SQL C collation. No PostGIS or central index is required. This scan is O(matches); optimize ranking in SQL if measured production latency cannot meet the existing deadline, without silently limiting candidate coverage.

### Rollout and monitoring

Deploy additive backend/migration first, allow one successful new-format refresh for every enabled covered provider/city, and then deploy/enable the new UI using existing flags. Migration only queues refresh; it does not block Startup on supplier calls. On rollback disable Catalogue flags and retain tables/history. The supported application rollback retains the new additive columns.

Meter `Refahi.Catalogue` exposes `catalogue.responses` (status, partial tags) and `catalogue.duration` in milliseconds. Existing executor logs contain source latency/failure type; Hotels logs matched counts, located candidates, selection/total duration and partialness. Worker logs retain provider/city publish counts and sync state. Coordinates, q and raw query strings are not logged. Framework request-start/finish diagnostics are suppressed at Information level on API/Web because they include the query. Review reverse-proxy/access-log and external analytics configuration before public rollout as those are outside these repositories.

### Local verification — 2026-10-23

Both full solutions build in Release. PostgreSQL disposable historical upgrade fixture verifies additive migrations, unchanged historical identities and refresh requeue with preserved LastSuccessAtUtc. A synthetic 1,200-hotel fixture verifies the nearest hotel beyond an alphabetical prefix, 20/1,000 prefixes, count, stale/expired data, profile and inactive geography. Two nearby reads and a profile completed in about 316 ms total locally; this is not a production load claim.

Headless Edge against a separate Web host/local fixture API verifies SSR cards without JavaScript, province-only search, explicit geolocation and three-decimal origin, nearby distance, pagination and Back/Forward, mobile/desktop layout, zero browser-side result API requests and no JS runtime errors. Unit/contract tests cover missing/invalid GIS, multivenue one-card behavior, province/city relationships and failures. Existing hotel, Checkout, Shell and service-worker tests pass.

Live supplier coordinates, full refresh duration, representative production volume and concurrent response latency remain release gates. This work does not enable production flags or run migrations against application databases.

Final targeted verification: 21 backend Catalogue, 134 Hotels (including guarded PostgreSQL upgrade), 12 frontend Catalogue, 28 frontend Hotels, 5 Checkout, 25 Shell and 9 service-worker tests passed (234 total). Both full Release solutions build successfully. Browser QA uses fixture data; no live supplier or production-volume certification is implied.

Local QA cleanup: fixture Web/Python processes were stopped. Docker stopped responding during removal of the disposable `codex-catalogue-location-test` container; its cleanup could not be confirmed. Once the local Docker daemon responds, remove only that named QA container with `docker rm -f codex-catalogue-location-test`. No application database was used for tests.
