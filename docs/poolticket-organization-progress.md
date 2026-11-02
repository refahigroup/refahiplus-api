# PoolTicket organization implementation progress

Updated 2026-11-03. Implementation is incomplete; this is not a production release.

## Completed

- Created isolated API and Web worktrees on codex/poolticket-org-catalogue, preserving original feature branches and untracked geography files.
- Merged origin/main into both repositories; resolved host, project, middleware, configuration, rendering inventory and solution conflicts.
- Both full solutions built successfully after merge (API has existing warnings; Web zero warnings/errors).
- Preserved private Pools network-only routes in the asset-manifest-versioned service worker. All 10 worker tests passed, including offline rejection for private Pools routes.
- Corrected Pools Order verification to compare SourceAmountMinor rather than the customer final amount after Promotions. Added a regression covering valid discount, tampered source amount, owner and item price. 51 targeted backend tests passed.

## Required remaining implementation

1. Organizational v1 transport, organization token acquisition/check, all 14 documented operations and wire validation; no public-v2 fallback.
2. Provider-native tables and additive migrations, canonical geography mappings, validated identity transition, local content/session sync and Media integration.
3. PoolsCatalogueSource registration, shared ordering/filtering, freshness/partialness and readiness coverage.
4. Authenticated customer provisioning using Identity mobile, name کاربر رفاهی پلاس and selected pool native state/city.
5. Single-session new purchases, persist-before-send operation journal, reserve only after verified Orders payment, real provider vouchers, unknown-outcome recovery and original-allocation compensation for proven failures.
6. Historical internal voucher compatibility, prohibition of local redemption for provider vouchers, server-side cancellation capabilities.
7. Full customer Web changes, operational review page/endpoints, PDF and rendering inventories.
8. PostgreSQL integration, browser, financial, migration and staging verification; release security audit.

## Locked decisions

One shared provider organization account; retail SourceAmount from total_sale; supplier total_sale_poolticket stays private. New purchases contain one session. Paid successful provider reservations have no online cancellation. Unknown reserve outcomes are never automatically resubmitted or refunded. Successful staging evidence for money/time/token/reserve semantics is required before enabling sales.

## Current limitations

The running adapter and issuance are still the old general v2 implementation with the explicitly bypassed external reservation. Do not activate purchases or claim organizational booking is implemented. No application database migration, live provider call, deployment or source activation was performed.

Tests were run locally against controlled responses. PostgreSQL and browser suites have not been executed for this implementation.


## Local execution verification — 2026-11-03

Pools startup now invokes IDbTools.ApplyMigrations<PoolsDbContext>() independently of CatalogEnabled, matching the existing module startup pattern. The API was launched with ASPNETCORE_ENVIRONMENT=Local and DOTNET_ENVIRONMENT=Local against the existing localhost refahi-db configuration. Startup successfully checked pools.__EFMigrationsHistory; no pending Pools migration was reported. Existing Local worker settings were retained; PurchaseEnabled remains false.

API remains listening on https://localhost:5001 and http://localhost:5000. Web remains listening on https://localhost:5271 and http://localhost:5270, also with environment Local. Web SSR and WASM recognize Local as a local API configuration; the Web host enables static web assets for this environment. The added Local launch profile sets the API URL without embedding any credential.

Headless Edge used the real Local hosts and database, with no API interception or fixture server. Five mobile routes returned 200: /pools?view=pools, /pools?view=sessions, /pools/cart, /pools/checkout and /catalogue. Cart and checkout were checked after WASM initialized. No pageerror events or horizontal overflow at 390px were observed. The Pools catalogue API returned 200. Screenshots and the JSON report are in the Web worktree artifacts/local-browser directory.

This is a smoke check, not certification of purchase, login, provider freshness, or organizational reservation. Existing persisted Pools rows are old; live v2 saens/list returned 403 during the worker cycle. The organizational adapter and Pools Catalogue source remain unimplemented. Browser navigation /catalogue currently demonstrates the existing registered sources, not Pools registration.
