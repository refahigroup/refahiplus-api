# Entertainments / TouristPanel

ADR: TouristPanel moves from Commerce into the independent five-layer Entertainments module. Commerce remains available for Aabsar. Entertainments has no Commerce project or contract dependency. Orders is the sole payable aggregate and the existing Checkout/Wallet flow is reused.

Local manual acceptance checklist (Persian): [entertainments-local-manual-test.md](entertainments-local-manual-test.md).

## Interfaces and ownership

Public discovery: `/api/entertainments/programs`, `/programs/{ProgramKey}`, `/suppliers`, `/suppliers/{providerKey}/{supplierId}`, `/filters`. Current manifest requirements are read separately at `/programs/{ProgramKey}/requirements` when entering checkout (fresh provider metadata, no mutation); static discovery flags never decide which personal data is requested for a purchase. Availability and sale options: `/programs/{ProgramKey}/offers?start=yyyy-MM-dd&end=yyyy-MM-dd`. Canonical ProgramKey is an internal GUID; provider native IDs and account identity remain associated in `entertainments.touristpanel_programs`.

Authenticated workflow: `/cart`, `/cart/items`, `/checkout/contact`, `/checkout/sessions`, `/checkout/sessions/by-key`, `/checkout/sessions/{id}`, `/checkout/sessions/{id}/confirm`, `/purchases/{id}?revealTickets=false`, `/purchases/{id}/cancel`. All calls use the project's response envelope. Preserve the same idempotency key when recovering an interrupted request.

AdminOnly: GET `/admin/operations`, GET `/admin/operations/{fulfillmentId}`, POST `/admin/operations/{fulfillmentId}/recheck`, POST `/admin/operations/{fulfillmentId}/resolve`. Resolve accepts `outcome` (`fulfilled` or `notcreated`), `evidence`, `providerOrderCode`, optional `tickets`, and `idempotencyKey`. Operator identity comes from authentication; evidence is encrypted. Replays must contain the same content. Recheck never creates another provider purchase.

Uncertain unpaid reservations: GET `/admin/reservations`, POST `/admin/reservations/{sessionId}/resolve` with `outcome` (`notcreated`, `expired`, or `released`), `evidence`, and `idempotencyKey`. This records an operator-certified provider outcome and ends only an unattached uncertain session; it never creates, retries or refunds a purchase. User/session locks serialize it with active checkout. Existing paid purchases cannot use this path. After resolution the owner can start a new reviewed attempt. Evidence remains encrypted in the provider-prefixed session table.

Geography: GET and PUT `/admin/location-mappings`. PUT accepts `{ "code": "provider-location-code", "cityId": 123 }`; city and province must be active in References. An unmapped location remains discoverable without a geographic filter. Mapping codes must come from received provider programs. Never infer References IDs from provider numbers or fuzzy names.

Catalogue registers source `entertainments` when Enabled, CatalogueEnabled, and the provider are enabled. IDs are `entertainments:{ProgramKey:N}`, kind `entertainment`, price null. Discovery reads local normalized public profiles. Current provider contracts have no GIS: nearby results carry no fabricated distance and follow all coordinate-bearing items. Static category labels and financial CategoryCodes are separate.

## Persistence and privacy

Business tables: carts, cart_items, checkout_sessions, purchases, purchase_items. Native reservation context is entity-split into touristpanel_session_reservations and touristpanel_reservations; purchase-line native payload is in touristpanel_purchase_item_contexts. Read models, location mappings, sync states, issuance, operation attempts, encrypted receipts and tickets use touristpanel_ prefixes. Historical Commerce migrations are untouched. No legacy orders or encrypted payloads are imported.

DataProtection purpose is `Refahi.Entertainment.Secrets.v1`. Persist and share the host's DataProtection key ring across instances. Retention is 90 days by default, and excludes active/ambiguous purchases. Reservation/purchase checkpoints containing manifest data are redacted only after their session or purchase has reached a terminal state.

## Configuration and staged activation

Move environment/secret-store overrides from `Commerce:Providers:TouristPanel` to `Entertainments:Providers:TouristPanel`. Existing JSON provider settings have been moved, without changing account, markup, CategoryCodes or financial gates. There is no old-prefix fallback. Keep secrets outside tracked configuration.

Defaults: `Entertainments:Enabled=false`, `CatalogueEnabled=false`, `DiscoveryWorkerEnabled=false`, MaxDiscoveryAgeDays=7, RecipientRetentionDays=90, ReconciliationMinutes=30. Provider `Enabled` and `SalesEnabled` retain their separate meanings. New sales require every existing contract/finance gate, including explicit markup/version, IRR price category allowlist, settlement confirmation, category mapping, timezone and allowed delivery hosts.

For the `Local` environment, `appsettings.Local.json` is ignored by Git and must set `Entertainments:Providers:TouristPanel:SalesEnabled=false` while only connection credentials are configured. Keep provider `Enabled=true` for catalogue sync/discovery. Enabling sales before completing the financial gates fails startup in `TouristPanelOptions.Validate()`; do not bypass that validation. `Local` loads `appsettings.json` plus `appsettings.Local.json`, not `appsettings.Development.json`.

1. Inventory and settle old TEST TouristPanel sessions/orders in Commerce before removing the old application version; expired/ambiguous external reservations need operator confirmation. Do not migrate them or delete Aabsar data.
2. Apply Entertainments migrations using the deployment's normal migration process. They create the new schema/tables and audit columns, explicit client-generated key metadata and a data-preserving session-reservation split; they never move/delete existing Commerce data. Startup discovery never waits for provider downloads.
3. Enable provider connection and DiscoveryWorkerEnabled, keep SalesEnabled false. A one-minute worker refreshes membership/details every 15 minutes and retries failures after 15 minutes. An account advisory lock prevents multiple instances publishing concurrently. Details are fetched conservatively in sequence (never above the planned maximum concurrency of two).
4. Configure Media:Persistent storage and the existing external-media AllowedHosts for verified TouristPanel image hosts (the current ImageBaseUrl host is files.0tp.ir). Images are downloaded only by sync; failed images use no-image UI.
5. Complete location mappings. Validate list count, publication age, missing profiles, safe text and images. A failed complete-list request preserves the previous membership. A fully repeated consecutive provider page is the provider-specific end marker; partial overlap and invalid identities are rejected. Detail failures preserve the prior valid profile.
6. Enable Entertainments:Enabled and CatalogueEnabled and the web application's Entertainments:Enabled link. Test anonymous HTML, text/geography filters, old public redirects, date-free profiles, and mixed Hotel/Entertainment pagination. Stale profiles remain visible up to seven days; initial preparation and expired coverage are unavailable/partial rather than a falsely complete empty inventory.
7. Run controlled provider-approved service-type 1 and 2 test sales before enabling public SalesEnabled. Compare reserve expiry, actual price, wallet charge, invoice and delivered ticket count/identity. Record measurements for the actual inventory and deployment load. These live checks have NOT been executed by the coding task.

Rollback: turn SalesEnabled off first, then hide the public UI/source as required. Keep credentials, workers and the compatible application version for existing paid purchases. Existing purchase and session-continuation APIs remain available when public discovery is disabled. Never roll back to a binary unable to process real Entertainments purchases.

## Issuance and recovery

Purchase states: AwaitingPayment, IssuancePending, Issuing, AwaitingResult, Completed, ManualReview, CompensationPending, Cancelled/Refunded. A paid Orders event queues issuance, and a bounded sweep recovers paid orders whose event execution was interrupted. Source identity, owner and line totals are checked.

Persist checkpoints before mutations. An ambiguous reserve is recovered by the same session key; do not start another reserve. An ambiguous finalize is NEVER PATCHed again. Only query a trusted stored invoice. A missing invoice needs manual review. PaymentWaiting, partial tickets, mismatched costs/manifest or lost responses never prove non-issuance and never trigger automatic refund. Definitive non-issuance uses Orders cancellation/compensation. Issued TouristPanel tickets do not have automatic cancellation.

The provider runbook references `TouristPanel-Production-Implementation.md`; that file was not found. Current wire behavior is preserved and fixture-tested. Live API/financial-contract verification remains a release gate.

## Verification

Run both solution builds and Entertainments, Commerce/Aabsar, Catalogue, Hotels, Checkout, Wallet and PWA regression tests. PostgreSQL test is opt-in via `REFAHI_ENTERTAINMENTS_TEST_CONNECTION`; the guard requires localhost:55442 and database prefix `entertainments_test_`. Use a disposable container only. It tests initial/upgrade migrations, persistent identity, sync failure preservation, local discovery, unknown GIS, expiry and SQL ownership of private native payloads.

## Implementation verification — 2026-10-26

- Backend and client solution Debug builds passed. Client Release publish passed; all four Entertainments WASM assemblies, visibility module, and generated service-worker asset manifest were verified in the publish artifact.
- 52 Entertainments backend tests passed, including actual disposable PostgreSQL initial/upgrade migration, session-reservation backfill, persistent native identity, atomic sync failure handling, program expiry, canonical GUID formats, current manifest requirements, issuance/ticket insertion, paid-event recovery, manual compensation, and audited unpaid-reservation recovery.
- 22 Entertainments frontend contract tests passed. Regression suites passed: backend Commerce 22, Catalogue 21, Hotels 141 (one unrelated PostgreSQL fixture skipped), Orders 17, Wallets 10; frontend Commerce 21, Checkout 5, Catalogue 12, Hotels 31, Shell 25. Nine service-worker tests passed.
- Headless Edge with an isolated local API fixture verified complete no-JavaScript listing/profile HTML, legacy identity/date redirects, exactly one browser offer request, no Commerce calls, mobile layout, login before cart mutation, and no JavaScript runtime errors. Desktop/mobile screenshots and the report are in the local temporary QA directory `entertainments-transfer-qa`.
- Production flags remain disabled. No live provider sale, production data migration, or production-volume load measurement was performed. The provider contract and staged activation checks above remain release gates.
