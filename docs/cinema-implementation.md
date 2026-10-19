# Cinema and theater integration

## Architecture and deployment

Cinema owns reservation/issuance snapshots and provider attempts in the `cinema` schema. Orders owns payable orders; Wallets is accessed only by Orders. Customer payment precedes reseller confirmation. The reseller wallet at iTicket is a separate supplier account.

Backend API prefix: `/api/cinema`. Cinema uses exactly five existing projects. The new frontend module uses Domain/Application/Infrastructure/UI, shared Landing components and the existing Checkout. Rendering inventory is in the webapp checkout at `docs/page-rendering-inventory.md`.

The five required architecture and rendering documents are available in `C:/Workspace/Refahi/docs` and were reviewed for the landing correction.

## Configuration

All Cinema flags default false. Configure `Cinema:CatalogEnabled`, `Cinema:PurchaseEnabled`, and `Cinema:CancellationEnabled` separately. Disabling new purchases does not stop reconciliation. Existing issued orders remain readable.

Set `iTicket:BaseUrl=https://console.iticket.ir/api/v1/` and supply `iTicket:AccessToken` through environment variables or secret storage; never in a committed file. Set category ULIDs in `Cinema:iTicket:CinemaCategories`, `TheaterCategories`, and `ArtCategories`. The baseline mappings verified on 2026-10-07 are Iranian cinema (`01kycvfgfdpy45wp3h61ydhkbd`), comedy theater (`01kyy0f2a7scgznmk1n63rf1pf`), and art/experimental cinema (`01kyypwh33rwgsn02hapecvxcn`). Use leaf categories: the supplier returned unrelated shows when filtering the theater parent ID. Empty mappings return empty sections and log a warning. Environment overrides, including the ignored `appsettings.Local.json`, must contain the same mappings or explicitly chosen replacements. The verified home banner placement is `home-1`; `home` returns no banners. Configure banner placement and provider status vocabulary from the supplier contract. The supplied default vocabulary is reserved/confirmed/cancelled and must be verified on the actual reseller account.

`Cinema:SafetySeconds` defaults 60 (minimum 60). PayableUntil is provider expiry, capped by purchase-end, minus this margin. Missing provider expiry never produces a payable order. `WorkerSeconds` defaults 15 (minimum 5). All amounts are long IRR; IRT is multiplied by 10 with checked arithmetic. Unsupported currencies fail closed.

Apply generated migrations using the normal host migration mechanism, or generate a reviewed SQL script:

```sh
dotnet ef migrations script --project src/Refahi.Modules.Cinema.Infrastructure --startup-project src/Refahi.Modules.Cinema.Infrastructure --context CinemaDbContext --idempotent
```

PDF uses an embedded Estedad font and Microsoft.Playwright Chromium. Both Dockerfiles install the browser and native dependencies into `/ms-playwright`. Local setup after build:

```sh
node tests/Refahi.Modules.Cinema.Tests/bin/Debug/net10.0/.playwright/package/cli.js install chromium
```

## Operational recovery

### Paid orders blocked before supplier confirmation — 2026-10-19

The production stack trace showed `DbUpdateConcurrencyException` while saving the new `confirm` attempt, before calling iTicket. Attempt IDs are assigned by the domain, but EF's generated-key convention tracked attempts added to existing orders as Modified instead of Added. EF tried to update a nonexistent attempt; retries could continue until the supplier reservation expired. The same mapping also prevented new cancellation attempts from being saved.

`CinemaProviderAttempt.Id` now uses `ValueGeneratedNever`; the model snapshot is aligned. This changes EF tracking only and requires no database schema migration. Deploy the rebuilt API. Tracking tests cover both confirm and cancel, and a PostgreSQL regression saves, reloads and completes both operations on existing paid orders. Before the fix, the PostgreSQL regression reproduced the production concurrency exception.

Deployment resumes background reconciliation for existing failed orders, which can now reach the supplier cancellation path and, on authoritative cancellation, the Orders refund path. This fix does not replay confirmation for failed orders or directly refund payments. Reconcile affected orders with the supplier and verify their financial state after deployment.

### Cancellation state conflicts — 2026-10-19

The official [Orders list contract](https://console.iticket.ir/docs/api/reference#/operations/v1.orders.index) includes `expired` in its status filter vocabulary (machine-readable [OpenAPI](https://console.iticket.ir/docs/api/openapi.json)). Reseller reserve holds seats without charging the seller wallet; confirm performs that charge. We treat an authoritative `expired` reservation returned by reseller lookup as terminal for Orders compensation, without another cancel POST. This is an integration interpretation of reservation expiry; the OpenAPI does not explicitly document refund behavior for expired reseller orders. Local deadline expiry alone remains insufficient.

The provider adapter recognizes `expired` through `ExpiredStatuses`. Terminal cancelled/expired lookup is reconciled even when new paid-ticket cancellations are disabled; that flag still guards new cancellation POSTs for active paid tickets. Pending and previously rejected cancellation attempts can recover after the supplier reports expiry. Regression tests cover these three states and replay. Unknown statuses log `ProviderStatus` together with Cinema/financial order IDs and still block refunds.

Cancellation now looks up the supplier reservation before submitting a cancellation POST. An authoritative cancelled status completes local cancellation even after an earlier rejected POST; a second POST is unnecessary. Pending cancellations and rejected attempts are never blindly reissued, and unknown statuses do not authorize refunds. The Orders API now distinguishes a supplier rejection from an ambiguous outcome. An expired local deadline alone does not prove supplier cancellation.

Deploy the rebuilt API; no admin UI change or migration is required. Existing affected orders can recover through the same Orders cancellation path when the supplier reports a configured cancelled status. A continuing rejection or unknown supplier status still requires supplier investigation. Lifecycle regression tests cover already-cancelled reservations, reconciliation after rejection, pending and unknown statuses, and the rejection message.

### Reservation 503 caused by provider timezone — 2026-10-18

The production log confirmed `Cannot write DateTimeOffset with Offset=03:30:00` in `CinemaDbContext.SaveChangesAsync`. iTicket returns timestamps with the Tehran offset; Npgsql accepts only UTC DateTimeOffset values for `timestamp with time zone`. A successful supplier reservation could therefore fail while persisting `PayableUntil`, and the subsequent failure-state save would fail on the same timestamp. The endpoint returned its generic 503.

`CinemaOrder.Reserve` now normalizes the deadline to UTC while preserving the instant, purchase-end cap and safety margin. Provider snapshots retain their original timestamps. Deploy the rebuilt API; no database migration or configuration change is required.

Unexpected endpoint failures now log the endpoint name, request trace ID, exception types and stack traces, and return the same trace ID in the error response. Exception messages, request bodies and raw supplier errors are excluded from this diagnostic event.

Existing affected reservation intents must follow the lost-reserve-response recovery below: the external reservation may exist even though its reference was not saved locally. Do not replay supplier reservation POSTs or reset idempotency keys automatically. Reconcile the supplier state before attempting another purchase.

Regression verification: the PostgreSQL test failed with the exact production exception before the fix and passes after it. Lifecycle tests cover UTC, Tehran and negative offsets through the Orders checkout handoff; endpoint tests cover safe correlated diagnostics, known rejections and cancellation propagation.

The local reservation intent is durable before the external POST. Every mutating provider operation has an attempt recorded as Started/Completed/Failed/Ambiguous. Reads may retry; mutations never retry blindly.

- Lost reserve response without a provider ID: NeedsReview, no second reserve. Locate the booking through supplier support using the schedule/customer context. Do not refund or invent a provider ID based on guesswork.
- Known provider ID after malformed reserve response: retain that reference and reconcile/release it; do not create an Order from invalid totals.
- Lost confirm response: query provider order. If confirmed, finish local issuance; otherwise keep reviewing without another confirm POST.
- Ambiguous cancel: retain Pending cancellation and query provider. Refund through Orders only when cancellation is authoritative. Failed cancellation retains a valid ticket and is not retried by the worker.
- Cancellation succeeded but wallet refund failed: retry the Orders cancellation path, whose refund key preserves original allocations.
- Financial Order creation succeeded but the local link failed: recover using `cinema-order:{cinemaOrderId}`.

All payment/cancellation paths acquire the Orders lock before the Cinema lock. Worker paths release Cinema before requesting Orders cancellation. Never invert this order. Pending provider actions are excluded from a new payment. The source lease spans the wallet Reserve/Capture and financial state persistence.

Logs deliberately exclude access tokens, customer mobile/name and raw supplier payloads/errors. Alert on warning events for CinemaOrderId, stalled paid issuance, cancellation pending and reconciliation errors. Invalid/unknown status or currency must be resolved with the provider, not converted into a guessed success.

## Release gate

Verify account access to cinema/theater/art categories, seller balance, expiry/status vocabulary, actual customer total and supplier charges, maximum seats, full cancellation/refund semantics and accepted entrance codes for both cinema and theater. No barcode is generated without a confirmed supplier format. Live purchase/refund testing is still required before enabling flags.

## Verification

```sh
dotnet build Refahi.Backend.slnx
dotnet test tests/Refahi.Modules.Cinema.Tests/Refahi.Modules.Cinema.Tests.csproj
CINEMA_BROWSER_TESTS=1 dotnet test tests/Refahi.Modules.Cinema.Tests/Refahi.Modules.Cinema.Tests.csproj
# CINEMA_TEST_POSTGRES must identify a disposable, isolated test database.
CINEMA_TEST_POSTGRES='...' dotnet test tests/Refahi.Modules.Cinema.Tests/Refahi.Modules.Cinema.Tests.csproj
```

The seat-map fixture was captured from the public sample page on 2026-10-07. It contains catalog/schedule/seat information only, no personal data. Browser and PostgreSQL tests are explicitly opt-in; the latter migrates the supplied test database and must not target production.

## Presentation contracts — 2026-10-07

CinemaShow adds optional Genres (id/name), ArtistDetails (id/name/portrait), SummaryHtml, DescriptionHtml and SummaryText, retaining legacy Artists/Summary/Description. CinemaPlace adds optional Cover. Older persisted seat-map/order snapshots remain deserializable; no schema migration is required. Deploy Backend before Frontend.

HTML is parsed and sanitized in Infrastructure using HtmlSanitizer 9.2.1039. Allowed tags are p/br/strong/b/em/i/u/ul/ol/li/a/blockquote, allowed attributes href/title, and allowed absolute link schemes http/https. Scripts, embedded active content, event attributes and styles are excluded. Plain synopsis text is extracted from sanitized DOM content. Frontend renders only sanitized HTML fields and escapes older text.

Artist portraits and missing place primary/gallery images are enriched through the existing provider client. Reads are coalesced through shared keyed locks and a five-minute cache; enrichment has a global concurrency limit of four. Complete embedded covers skip enrichment. Optional media failures degrade to poster/logo/avatar/theme placeholders and do not hide discovery content. Cancellation is propagated. Image URLs are restricted to HTTP(S); banner links use safe HTTP(S) or local paths.

The configured `home-1` placement was checked read-only and returns one banner. The adapter retains all valid returned banners in sort_order, maps safe links, and logs count/placement without tokens or raw payloads. Zero/one/three-banner mapping is covered by fixtures; no additional placements or fabricated slides were added.

Verification includes formatting/XSS, legacy contracts, genres/portraits, concurrent deduplication, missing-media degradation and complete-cover request avoidance. Existing order/expiry/idempotency/payment lifecycle tests and the real Playwright PDF test remain in the suite. The PostgreSQL migration/locking test requires an explicitly isolated test database and is not run against the configured application database.
