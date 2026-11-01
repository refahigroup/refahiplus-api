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

Defaults: `Entertainments:Enabled=false`, `CatalogueEnabled=false`, `DiscoveryWorkerEnabled=false`, MaxDiscoveryAgeDays=7, RecipientRetentionDays=90, ReconciliationMinutes=30. Provider `Enabled` and `SalesEnabled` retain their separate meanings. New sales require every existing contract/finance gate, including explicit markup/version, nonnegative discounts, settlement confirmation, timezone and allowed delivery hosts. IRR price selection is automatic; missing financial category mappings use `entertainment`.

For the `Local` environment, `appsettings.Local.json` is ignored by Git. Connection-only setups keep `SalesEnabled=false`; the explicitly enabled Local supplier-retail setup below is a separate choice. Keep provider `Enabled=true` for catalogue sync/discovery. Enabling sales before completing the financial gates fails startup in `TouristPanelOptions.Validate()`; do not bypass that validation. `Local` loads `appsettings.json` plus `appsettings.Local.json`, not `appsettings.Development.json`.

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

## Cheapest supplier price and retail adjustments — 2026-10-29

Each ticket type produces one public option. The provider selects the lowest positive integral IRR `buyPrice` with a valid nonempty price-category GUID, then the normalized category GUID ordinally breaks ties. Conflicting duplicate category payloads disable that type. Equal duplicates are collapsed. Invalid prices and free supplier tickets cannot be bought. `AllowedPriceCategoryIds` is retired: remove it from environment overrides; a remaining legacy value is ignored. A returned price-category identity is provider context, never a customer choice.

`MarkupPercent` and `MarkupFixedMinor` remain explicit, with `PricingVersion` required. `DiscountPercent` and `DiscountFixedMinor` default to zero. All four inputs are nonnegative. Both percentage amounts use supplier cost as their base and are rounded per ticket to IRR with AwayFromZero. Retail = cost + rounded markup + fixed markup - rounded discount - fixed discount. A positive price below cost is permitted; zero/negative retail and overflow are unavailable. Update `PricingVersion` whenever these rules change. Supplier `ticketPrice` and commission are retained as audit evidence, not used as a strike-through price or retail base. No new database migration is required.

Stable SHA-256 v2 option identities include program, event and ticket type, but exclude the price category. Existing cart hashes are resolved against currently returned legacy category identities. Adding a canonical selection merges matching old rows, preserves the canonical row ID when present (otherwise the first matching legacy row ID) and replaces its quantity rather than incrementing it. Reservation review merges old rows of the same canonical ticket type, preserving summed quantities and all passenger data, and rejects conflicting fresh quotes or insufficient combined capacity before any provider mutation. Original cart line IDs remain in the checkout selection for exact cleanup on confirmation. An unresolvable old selection remains in the cart with an instruction to remove/reselect. Reserved snapshots and paid purchases are never repriced; their pinned native identity and supplier cost remain authoritative during issuance. Missing historical `DiscountMinor` reads as zero. Missing category mappings fall back to `entertainment`, with existing explicit mappings taking priority; Wallet remains the authority for allocation eligibility.

The public option adds nullable `UnavailableReasonCode`: `PriceUnavailable`, `TicketDisabled`, `SoldOut`, or `SalesUnavailable`. The UI presents customer messages, hides an unnecessary selector for exactly one buyable type, supports read retry and requires another explicit click after an add-to-cart HTTP 409 supplies a new price. No cart mutation is automatically retried. A missing retail rule is different from a free ticket: neither is purchasable. Sales and contractual gates remain enforced; this change does not activate them. Aggregate unavailable counts, conflicting price identities, and reservation price differences are logged without passenger data.

Read-only live samples from four services are normalized into `tests/Refahi.Modules.Entertainments.Tests/Fixtures/touristpanel-live-prices.json`. Fixtures contain no credentials, tokens or customer data. Before activating public sales, run controlled provider-approved sales for service types 1 and 2 and validate actual expiry, wallet payment, invoice and delivery. Disabling new sales must still permit fulfillment/recovery of existing paid purchases.

Live contract detail: a zero GUID `referenceTicketTypeId` with `isNumberEqualsReferenceTicketType=false` means no reference dependency and is normalized to null. A required reference with a zero/missing ID is still rejected. This keeps valid live prices visible without weakening required-reference validation.

### Verification of cheapest-price update — 2026-10-29

- Entertainments backend: 83 passed, including the guarded disposable PostgreSQL migration/ownership test and real unique-index cart merge checks; none skipped. Orders 17, Wallets 10 and Commerce 22 regression tests passed.
- Frontend: Entertainments 35 (including actual component initialization/render-frame tests and HTTP 409 handling), Checkout 5 and Commerce 21 passed. All nine service-worker tests passed.
- Both Debug solutions built; Release web publish succeeded. Its service-worker asset manifest contains all four Entertainments WASM modules, CSS, infinite-scroll and visibility assets. Existing dependency advisories remain outside this change.
- The modified provider was exercised with live read-only APIs using an isolated, in-memory zero-markup/zero-discount scenario. It returned two buyable options for سرزمین افسانه‌ها and 48 options across six offers for فلای‌بورد کیش; a fresh quote succeeded for each service type. The harness prohibited all provider HTTP mutations except authentication. Host settings, credentials and sale flags were not changed. This is not proof of a successful real reservation, settlement or ticket issuance; controlled sale acceptance remains required before activation.

### Local image and unavailable-purchase diagnosis — 2026-10-29

Persistent image URLs use the host-root `/media/persistent/{id}` endpoint, like external images; they must not be nested under `/api/media`. Configure `ExternalMedia:PublicBaseUrl` as the reachable media host origin (Local: `https://localhost:5001`), enable `PersistentExternalMedia`, and use distinct absolute persistent/cache storage folders. The verified TouristPanel `files.0tp.ir` image host is allowlisted. Missing profile images require another catalogue detail sync after configuration is corrected; reloading the browser alone cannot populate the read model.

When no type is buyable, the purchase box shows an availability explanation rather than a disabled ticket-type selector. A disabled sale connection without explicit pricing/settlement remains unavailable; image configuration must never activate financial sales or invent markup/settlement values. In that case the current public profile's sale-disabled explanation takes precedence over a missing retail-price explanation.


## Local supplier-retail pricing — 2026-10-30

The requested Local setup explicitly enables `SalesEnabled` and `UseProviderSalePrice`. The customer unit price equals the selected TouristPanel `ticketPrice`, in IRR. `MarkupPercent`, `MarkupFixedMinor`, `DiscountPercent` and `DiscountFixedMinor` are explicitly zero, with `PricingVersion=local-touristpanel-retail-v1`. Provider `buyPrice` and commission remain unchanged for reservation/invoice reconciliation; the difference between supplier retail and cost is recorded as `ProviderRetailAdjustmentMinor`, not Refahi markup. The default mode (`UseProviderSalePrice=false`) continues applying the existing supplier-cost pricing rules in other environments. Do not change prices of existing reservations or orders.

Local settlement is configured as `PaymentMethod=100` (`Marketplace_Wallets`, the TouristPanel account wallet) with `BankGateway=0` (`None`); user payment still goes exclusively through Orders/Wallet. Existing validation, manifests, capacity, reservation expiry, idempotency and uncertain-issuance recovery stay enforced. Local timezone is `Asia/Tehran`. Delivery URLs remain restricted to the explicitly configured known provider API/file hosts. No provider purchase has been made to verify the account balance or live settlement; that remains an actual-sale acceptance check.

For an explicit date, display always reads `event/ticket-types`, the same authoritative endpoint used by quote/reservation. Embedded `events.ticketTypes` prices are not used for dated purchase choices: live Simorgh data returned different costs/commissions through the two endpoints for the same category. The no-date calendar remains lightweight.

To reproduce Local pricing without copying secrets, add these fields under `Entertainments:Providers:TouristPanel` in the ignored Local settings, alongside the account connection/settlement configuration:

```json
{
  "SalesEnabled": true,
  "UseProviderSalePrice": true,
  "MarkupPercent": 0,
  "MarkupFixedMinor": 0,
  "DiscountPercent": 0,
  "DiscountFixedMinor": 0,
  "PricingVersion": "local-touristpanel-retail-v1"
}
```

Restart the Local API after changing these settings because the provider uses startup-bound `IOptions`. Enabling this Local profile does not enable other deployment environments.

Verification of Local retail mode: API and web Debug builds succeeded; Entertainments 92 tests passed (the opt-in PostgreSQL test was skipped), Orders 17 and Wallets 10 passed. Mocked reservation/issuance tests cover both service types, unchanged native cost, zero Refahi markup/discount, rejection of invalid native retail amounts and changed cart retail, and differing embedded-versus-dedicated prices. The running Local API returned all seven Simorgh ticket types for 2026-10-30 as available at the panel retail prices (including parasail 3,300,000 toman and shuttle 1,100,000 toman); browser validation confirmed the populated ticket selector and enabled add-to-cart button. The API and web were restarted, with web assets rebuilt after resolving a stale resource-collection import. No real provider reservation, wallet debit or issuance was performed.
