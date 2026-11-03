# PoolTicket Bruno alignment — 2026-11-03

Reference: `C:/Workspace/Tools/Bruno/ORG PoolTicket API v1.zip`. Collection request definitions were compared with the adapter, sync, purchase, Catalogue and browser callers. Credentials were loaded into the ignored API Local settings; the archive and credentials are not committed.

## Contract comparison

All requests use form encoding, `Accept: application/json`, server-side client ID and organization token. The correct host is `https://staging.poolticket.org`.

| Operation | Implemented path | Contract handling |
|---|---|---|
| Password token | `/api/v1/token` | `grant_type=password`, username/password, scope=organization; coordinated token cache |
| Check token | `/api/v1/token` | `grant_type=check_token`, access and refresh token, organization scope |
| States | `/api/v1/org/state/list` | `state_name`; boolean or numeric envelope status |
| Cities | `/api/v1/org/city/list` | state association comes from request context |
| Areas | `/api/v1/org/area/list` | city ID and native area identity |
| Pools | `/api/v1/org/pool/list/{page}/{limit}` | total_pages; search_keyword/city/image_width/status_sell; no public-v2-only filters |
| Pool details | `/api/v1/org/pool/info/{id}` | validate pool identity; sanitize HTML; Media URLs; descriptions/address/telephone/facilities |
| Sessions | `/api/v1/org/saens/list` | pool + gender; flat rows, explicit times, nonnegative numeric capacity |
| Session info | `/api/v1/org/saens/info` | pool + saens_row_id; boolean or numeric availability |
| Calculate | `/api/v1/org/reservation/calculate` | fixed configured mobile, counts and row; total_sale is retail; supplier cost stays private |
| History | `/api/v1/org/reservation/history/{page}/{limit}` | fixed configured mobile; accept numeric-string limit observed live |
| Reservation info | `/api/v1/org/reservation/info` | fixed configured mobile and known reservation ID; validate returned identity |
| Registration | `/api/v1/org/auth/register` | configured customer fields; omit national code |
| Reserve | `/api/v1/org/reservation/reserve` | real voucher/reference; only after verified Orders payment; no transport retry |

Observed staging differences: sale flags can be integers; pool coordinates and max_discount are strings; absent coordinates are empty strings; session-info capacity is numeric despite the documented boolean; history limit is a numeric string. These are handled narrowly, without making generic price/status parsing permissive.

## Server configuration

`Pools:PoolTicket` owns:

```json
{
  "BaseUrl": "https://staging.poolticket.org",
  "ClientId": "<organization client>",
  "Username": "<organization login>",
  "Password": "<organization login password>",
  "user_name": "<customer display name>",
  "user_mobile": "09001769450",
  "user_password": "<separate customer password>",
  "user_state": "<native PoolTicket state ID>",
  "user_city": "<native PoolTicket city ID>"
}
```

`AccessToken` and `RefreshToken` can alternatively be supplied securely. Organization login is separate from the fixed booking customer. Refahi purchase ownership remains tied to the authenticated Refahi user. The upstream mobile is never accepted from the browser. Calculate/quote requires authentication. On an explicit `not_found_user`, register the configured customer, then retry the rejected operation once; an uncertain register response does not proceed to payment/reserve.

## Geographic ownership and Catalogue

New provider-owned tables: `pools.poolticket_contents`, `poolticket_geographies`, `poolticket_provinces`, `poolticket_sync_states`, `poolticket_reservations`. Common historical venue, purchase and request tables retain their identities and names. All migrations are additive and run through the existing startup `IDbTools.ApplyMigrations<PoolsDbContext>()` mechanism, independent of the catalogue feature flag.

Match native state/city names to References with Persian normalization and unique matches within the matched province. Native numeric IDs are never treated as canonical IDs. Two explicit observed province spelling variants are normalized. Duplicate native identities reject the sync. A first organizational venue migration also checks normalized existing name/state/city before reusing its public GUID.

Local evidence: 31/31 provinces mapped; 440 native city rows loaded, 376 mapped, 64 ambiguous/unmatched. All five cities used by the 28 available organizational pools are mapped; no active venue lacks a mapping. The [unmatched report](diagnostics/poolticket-unmapped-geography-2026-11-03.csv) includes county-like entries, renamed/missing towns and incorrect upstream province assignments. Per the user-confirmed policy, they remain unlinked for review; no county-to-capital substitution or cross-province guess is made.

`PoolsCatalogueSource` registers as `pools`, with `pools:{VenueId:N}`, kind `pool`, category `entertainment.waterpark`, and `/pools/venues/{VenueId}` links. Queries use local projections only. Canonical province/city filters, normalized text and nearby ordering run before prefix selection using CatalogueOrdering. Unknown sync and data older than seven days are unavailable; confirmed empty data remains a successful empty response. Staleness uses real sync timestamps. Missing content/geography is partial. MaxConcurrency is 24 for three sources and eight concurrent requests; deadlines are unchanged.

## Payment and recovery

New purchases accept one session, preserving adult/child counts. Existing idempotency replay is checked before enforcing the new restriction. No provider reserve occurs during purchase creation or checkout. After verified Order payment, payment state is persisted and a provider operation with Sending intent commits before HTTP. A crash or uncertain response cannot trigger another reserve. The supplier voucher is encrypted; it is not assigned an internal redemption-code hash.

Successful, rejected and unknown fulfillment are distinct from financial payment. Successful supplier reservations cannot be cancelled or redeemed through Refahi operators. Historical internal vouchers retain their behavior. Definite rejection enters Orders cancellation/refund with original wallet allocation. Recovery remains active when new sales/discovery are disabled. Unknown outcomes and amount/count mismatches require review and survive internal deadlines without auto-refund. Known reservation evidence is retained for mismatches. Operations store the fixed mobile used at send time. The public page continues polling paid-but-unresolved reservations and only offers PDF after vouchers exist.

Unknown operations currently require operational investigation; there is no automatic history-based attachment, resend control, or new support UI in this change. The archive does not document a supplier idempotency key or online cancellation contract.

## Verification and Local runtime

Backend: 119 tests passed, including real isolated PostgreSQL, Orders/Wallets split payments/refunds, concurrent fulfillment, crash/restart intent, ambiguous outcomes, live time changes before debit, contract forms, registration fallback and token coordination. The test database is isolated from Local business data.

Web: 30 browser/service/presentation tests passed. Browser and service checks cover date filtering, frozen drafts, single-ticket replacement, payment/reservation polling, authentication and published service-worker privacy. Real Edge Local check: 28 active venues; university venue has 78 session rows; 6 tickets for 2026-11-06; no stale notice, JS errors or mobile overflow; full details render without JavaScript. Canonical Mashhad Catalogue filtering returns 15 Pools items. Evidence is in the Web worktree `artifacts/pool-bruno/report.json` and screenshots.

Live read-only staging checks succeeded for token login/check, states/cities/areas, venue list/details, sessions/info, calculate, the configured customer's history, and a known reservation info. Reserve and registration fallback are verified with contract tests, not a new live paid reservation. `PurchaseEnabled=false` remains in Local. Confirm reserve financial semantics and complete a controlled staging purchase before enabling sales.
