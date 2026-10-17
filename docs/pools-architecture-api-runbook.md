# Pools — architecture, API and operational runbook

Updated 2026-10-13. Supersedes the historical mock implementation notes.

## Ownership and architecture

The five backend projects and PostgreSQL `pools` schema remain. Independent handlers/validators use `Features/{Feature}/{Action}`. Domain owns typed purchase/voucher states and immutable purchase snapshots. Infrastructure owns repositories, real PoolTicket adapter, locks, workers, encryption and PDF. Cross-module production references are Orders and Identity Application.Contracts; Pools never calls Wallet directly.

Catalogue tables `venues` and `sessions` map stable public GUIDs to private provider/external identifiers. Composite provider/external keys are unique. Database queries filter, order with a GUID tie-breaker, page and count actual results. GET catalogue/purchase never calls upstream or performs reconciliation. Purchase history uses its own immutable snapshot.

`purchases` owns user/key/fingerprint/deadline/total/version and one unique OrderId; `requests` owns selected session, counts, snapshot, UTC consumption range, encrypted code/hash and redemption audit. Each request becomes one OrderItem, Quantity=1, SourceItemId=RequestId, category `entertainment.waterpark`. SourceModule=Pools, ReferenceType=PoolPurchase, SourceReferenceId=PurchaseId. Codes use 128-bit cryptographic randomness; database uniqueness and the purchase lock prevent duplicate issuance/consumption.

Upstream TOMAN is converted with checked multiplication by ten; internal financial values are long IRR. Negative, fractional-unrepresentable and overflowing values fail. Frontend displays toman. Only PoolTicket is registered; additional providers require a backend adapter, without exposing provider IDs in public contracts.

## API

Prefix `/api/pools`. JSON uses unified envelopes, Persian messages, cancellation tokens, named/tagged endpoints. Public IDs are GUIDs.

| Method | Route | Access |
|---|---|---|
| GET | `/catalog/pools`, `/catalog/pools/{id}` | Public |
| GET | `/catalog/sessions`, `/catalog/sessions/{id}` | Public |
| GET | `/catalog/states`, `/catalog/cities?state=...` | Public |
| POST | `/quotes` | Public; live validation |
| POST | `/purchases` | UserOrAdmin; owned purchase |
| GET | `/purchases/{id}` | Owner; read-only |
| POST | `/purchases/{id}/checkout`, `/cancel` | Owner |
| GET | `/purchases/{id}/vouchers.pdf` | Owner; PDF or unified JSON error |
| GET | `/operator/pools` | Authenticated; active grants |
| POST | `/operator/vouchers/validate`, `/redeem` | Active matching pool grant; VoucherRedeem rate limiter |
| GET/POST | `/admin/operators` | AdminOnly |
| DELETE | `/admin/operators/{grantId}` | AdminOnly |
| GET | `/admin/catalog-status` | AdminOnly; database diagnostics |
| POST | `/admin/legacy-cleanup` | AdminOnly; Development/Test/Staging only |

Filters: search/state/city/gender(0/1)/sort(name/date/price/rating)/page/limit(1..50); sessions add poolId and ISO Gregorian date. Total is a database count.

Quote body: `{ "choices": [{ "sessionId": "guid", "adultCount": 1, "childCount": 0 }] }`. Purchase adds expectedTotalMinor and idempotencyKey(max128). There are 1..20 distinct sessions and 1..100 people per request. Checkout body contains version and totalMinor.

Validation body: poolId/code. Redemption adds idempotencyKey. Codes are sent only in POST bodies. Grant body: userId/poolId; user must already exist.

Stable error codes include PRICE_CHANGED(with new quote details), IDEMPOTENCY_MISMATCH, SESSION_UNAVAILABLE, SESSION_TIME_CHANGED, POOL_NOT_READY, PURCHASE_CHANGED, ORDER_SOURCE_MISMATCH, VOUCHER_NOT_REDEEMABLE, REDEMPTION_FORBIDDEN and PDF_UNAVAILABLE. Only PRICE_CHANGED permits new confirmation/key; unknown/network outcome and unrelated409 preserve the original request.

Purchase states: AwaitingPayment/Paid/CancellationPending/Cancelled/Refunded. Voucher states: Issued/Redeemed/Revoked/Expired. Expiry is immediately derived in reads and persisted through recovery. ReservationStatus explicitly reports ExternalReservationBypassed.

## Configuration and rollout

Default CatalogEnabled/PurchaseEnabled are false. Local retains its existing catalogue flag; purchase remains false. Runtime fixture/lifecycle/decorator and mock options have been removed.

```json
{
  "Pools": {
    "CatalogEnabled": false,
    "PurchaseEnabled": false,
    "ReservationMinutes": 10,
    "WorkerSeconds": 15,
    "VenueSyncSeconds": 1800,
    "SessionSyncSeconds": 300,
    "SyncConcurrency": 2,
    "MaxCatalogPages": 10000,
    "PoolTicket": {
      "BaseUrl": "https://staging.poolticket.org",
      "TimeoutSeconds": 30,
      "RequestEncoding": "Form",
      "PriceUnit": "TOMAN",
      "CalculateReadOnlyConfirmed": false
    }
  }
}
```

ClientId belongs in server secret store or Pools__PoolTicket__ClientId. Confirm production BaseUrl and harmlessness of reservation/calculate with provider before activation. Interval changes use IOptionsMonitor next cycle; invalid options fail validation. No automatic external write retry; circuit breaker protects the client.

Provider requests send `Accept: application/json`; automatic HTTP redirects are disabled. A 3xx response produces `POOLS_PROVIDER_REDIRECT`, and HTML produces `POOLS_PROVIDER_NON_JSON` (both HTTP 502). Inspect the operation, provider pool ID and HTTP status in server logs; verify the pool exists and the provider credentials/API route are valid. Do not parse website HTML as catalogue data or treat this failure as an empty session list. Logs exclude raw response bodies and redirect locations, which can contain tokens.

Before purchases, verify genuine provider samples, prices, exact start/end time, PDF runtime, key persistence and operators. Active Pools-issued grant is required for every selected pool. Unknown/ambiguous time ranges cannot be purchased. Accepted dates: Persian yyyy/MM/dd (Persian digits supported) or Gregorian ISO. Times require both HH:mm - HH:mm or HH:mm تا HH:mm; unknown/overnight ranges fail closed. Conversion uses Asia/Tehran. Consumption is start inclusive, end exclusive.

Persist/share the existing Data Protection key ring across restarts/hosts with DataProtection:KeysPath and restrictive access. Preserve existing application discriminator and historical keys; missing keys make stored codes unreadable. Install Chromium using the Playwright script in the Infrastructure build output. PDF embeds a local font, blocks external requests and limits concurrent rendering to two.

## Sync, financial recovery and consumption

Venue sync collects every page before saving/deactivating. Duplicate pages/IDs, incomplete payload, page bound or interrupted HTTP abort without removing old projection. Session/terms fetch completes before changes for that venue; one venue failure does not stop the rest. PostgreSQL advisory locks coordinate hosts, bounded parallelism uses separate scopes. IDs remain stable. Show FetchedAt, but purchase/payment still use live quote/time validation.

Admin catalogue diagnostics returns Disabled/NotConfigured/NeverSynced/Degraded/Ready, counts, last success and stale venue/session counts at twice configured interval. Structured logs include latency, provider trace, purchase/order/request/operator IDs. Secrets, full codes and sensitive raw payload are not logged.

User/key uniqueness and fingerprint normalize choices+confirmed total. Order key pools-purchase:{PurchaseId:N} repairs interruption between Order commit and attachment. Deadline defaults to ten minutes and cannot pass earliest session start. Persisted payload/amount never changes.

Lock order is Orders financial then Pools source. Payment validates owner, full item/amount/source matching, deadline and live quote, then checks deadline again. Paid event/recovery verifies actual Orders payment before atomic issuance of all codes. No issuance based solely on event/browser payload.

Cancellation preparation rejects any consumed voucher, revokes remaining vouchers under the source lock, saves CancellationPending, then allows Orders refund of original allocations. Recovery recognizes BOTH Cancelled and Refunded Orders states. Synchronous cancelled-event handling must not recursively acquire the financial lock. Refund failure keeps revoked codes and retriable state. No partial refund or administrator override.

Consumption checks active Identity AuthorizationGrant issuer Pools, value pool:{PoolId:N}:redeem, without a global-role shortcut. Operator/key replay returns prior result; changed payload conflicts. Grant is rechecked under purchase lock. Consumption and refund contend on that same lock.

## Migration and legacy cleanup

Apply new PoolsCataloguePurchasesAndVouchers migration; applied migrations were not rewritten. New tables/indexes are added; an empty mock_reservations table is removed. A nonempty legacy table remains until financial cleanup. Old bookings/provider_attempts/catalog_snapshots are persistence-only; old creation/fulfilment handlers are absent. No Ledger, Commerce or other-module schema is changed.

Explicitly call Admin POST /api/pools/admin/legacy-cleanup in an allowed test environment. It resolves every historical Pools Order, cancels through Orders, verifies terminal Cancelled/Refunded and no Paid/Reserved state, then removes only mock Pools bookings. Nonmock rows, missing financial references or failed settlement stop cleanup. Final empty check clears old catalogue snapshots/mock table. There is no startup data cleanup.

Rollback: disable PurchaseEnabled first, retain catalogue/recovery for outstanding financial work. Preserve new tables and key ring. Application rollback is not a destructive database downgrade.

## Frontend and verification

Both rendering inventories register canonical/legacy routes. Discovery/details are SSR; count selection is a WASM island. Cart/checkout/purchase/operator/admin use WASM prerender=false and existing browser auth/design system. Cart/draft use tab-local sessionStorage; submitted payload/key freezes. Shared Orders Checkout/Profile routes use canonical purchases. Private navigation/PDF/API are network-only/no-store; worker cache generation invalidates old pages. Legacy discovery redirects and old booking links show a controlled message.

Backend tests cover provider contracts, money/time/domain, actual PDF/crypto, isolated PostgreSQL sync/idempotency/payment/refund/consumption/concurrency and HTTP API. Financial integration uses actual Orders/Wallets; doubles exist only in test projects. Set REFAHI_POOLS_TEST_CONNECTION to an isolated database whose name contains test. Frontend Chromium uses a test-only API server at55732 and Development UI host at55733; set POOLS_UI_URL. It covers SSR without JS, mobile layout, grouped checkout, lost result/typed conflict, invalid draft, operator consumption and real published SW.

Local connection is used only to create an isolated test database. Application data is not migrated/seeded by the integration suite. Genuine PoolTicket sync/production activation remains a rollout requirement; tests make no live PoolTicket requests.

## Explicit temporary limitation

PoolTicketReservationGateway returns true without external HTTP. Payment and internal Refahi vouchers are real; PoolTicket capacity is NOT reserved. No fabricated external reference/capacity confirmation is generated. UI/PDF and request status disclose this limitation. External booking, partial consumption/cancellation, administrator override, mixed-module orders and Aabsar migration remain outside this phase.

## Validation result — 2026-10-13

Both full Solutions build successfully (frontend zero warnings/errors; backend existing warnings outside Pools). Pools backend:43 passed, zero skipped, including real isolated PostgreSQL and Chromium PDF. Pools frontend:16 passed, zero skipped, including browser/PWA. Orders13, Wallets10 and Shell24 tests passed. EF reports no pending model changes. No Checkout/Profile standalone test project exists; shared checkout and Profile route registration are covered by browser tests/build. No live PoolTicket call or production activation was performed.
