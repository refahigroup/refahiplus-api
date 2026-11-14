# Pools — architecture, API and operational runbook

Updated 2026-11-03. Organizational contract and current Local evidence: [PoolTicket Bruno alignment](poolticket-bruno-alignment.md). Historical internal vouchers remain supported.

## Ownership and architecture

The five backend projects and PostgreSQL `pools` schema remain. Independent handlers/validators use `Features/{Feature}/{Action}`. Domain owns typed purchase/voucher states and immutable purchase snapshots. Infrastructure owns repositories, real PoolTicket adapter, locks, workers, encryption and PDF. Cross-module production references include Orders, Identity, Catalogue, References and Media Application.Contracts; Pools never calls Wallet directly.

Catalogue tables `venues` and `sessions` map stable public GUIDs to private provider/external identifiers. Composite provider/external keys are unique. Database queries filter, order with a GUID tie-breaker, page and count actual results. GET catalogue/purchase never calls upstream or performs reconciliation. Purchase history uses its own immutable snapshot.

`purchases` owns user/key/fingerprint/deadline/total/version and one unique OrderId; `requests` owns selected session, counts, snapshot, UTC consumption range, encrypted code/hash and redemption audit. Each request becomes one OrderItem, Quantity=1, SourceItemId=RequestId, category `entertainment.waterpark`. SourceModule=Pools, ReferenceType=PoolPurchase, SourceReferenceId=PurchaseId. Historical internal codes use 128-bit cryptographic randomness. New organizational requests use the encrypted supplier voucher, without an internal redemption-code hash. The purchase lock and durable provider Sending intent prevent duplicate reserve.

Upstream TOMAN is converted with checked multiplication by ten; internal financial values are long IRR. Negative, fractional-unrepresentable and overflowing values fail. Frontend displays toman. Only PoolTicket is registered; additional providers require a backend adapter, without exposing provider IDs in public contracts.

## API

Prefix `/api/pools`. JSON uses unified envelopes, Persian messages, cancellation tokens, named/tagged endpoints. Public IDs are GUIDs.

| Method | Route | Access |
|---|---|---|
| GET | `/catalog/pools`, `/catalog/pools/{id}` | Public |
| GET | `/catalog/sessions`, `/catalog/sessions/{id}` | Public |
| GET | `/catalog/states`, `/catalog/cities?state=...` | Public |
| POST | `/quotes` | UserOrAdmin; private live validation using configured upstream customer |
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

Quote body: `{ "choices": [{ "sessionId": "guid", "adultCount": 1, "childCount": 0 }] }`. Purchase adds expectedTotalMinor and idempotencyKey(max128). New purchases contain exactly one session and 1..100 people. Historical grouped purchase replay is preserved. Checkout body contains version and totalMinor.

Validation body: poolId/code. Redemption adds idempotencyKey. Codes are sent only in POST bodies. Grant body: userId/poolId; user must already exist.

Stable error codes include PRICE_CHANGED(with new quote details), IDEMPOTENCY_MISMATCH, SESSION_UNAVAILABLE, SESSION_TIME_CHANGED, PURCHASE_CHANGED, ORDER_SOURCE_MISMATCH, VOUCHER_NOT_REDEEMABLE, REDEMPTION_FORBIDDEN and PDF_UNAVAILABLE. Only PRICE_CHANGED permits new confirmation/key; unknown/network outcome and unrelated409 preserve the original request.

Purchase states: AwaitingPayment/Paid/CancellationPending/Cancelled/Refunded. Voucher states: Issued/Redeemed/Revoked/Expired. Expiry is immediately derived in reads and persisted through recovery. ReservationStatus reports Pending/Reserved/Rejected/Unknown for organizational purchases; ExternalReservationBypassed is historical. Paid financial status alone does not mean a supplier ticket exists.

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

Lock order is Orders financial then Pools source. Payment validates owner, full item/amount/source matching, deadline and live quote, then checks deadline again. Paid event/recovery verifies actual Orders payment. Organizational requests commit a provider Sending intent before reserve and persist supplier vouchers; historical bypassed requests retain atomic internal-code issuance. No issuance based solely on event/browser payload.

Cancellation preparation rejects consumed historical vouchers and reserved/unknown/paid-pending external reservations. Unpaid intents or definite external rejection may enter CancellationPending; Orders performs refund using original allocations. Recovery recognizes BOTH Cancelled and Refunded Orders states. Synchronous cancelled-event handling must not recursively acquire the financial lock. Refund failure keeps revoked codes and retriable state. No partial refund or administrator override.

Consumption checks active Identity AuthorizationGrant issuer Pools, value pool:{PoolId:N}:redeem, without a global-role shortcut. Operator/key replay returns prior result; changed payload conflicts. Grant is rechecked under purchase lock. Consumption and refund contend on that same lock.

## Migration and legacy cleanup

Apply new PoolsCataloguePurchasesAndVouchers migration; applied migrations were not rewritten. New tables/indexes are added; an empty mock_reservations table is removed. A nonempty legacy table remains until financial cleanup. Old bookings/provider_attempts/catalog_snapshots are persistence-only; old creation/fulfilment handlers are absent. No Ledger, Commerce or other-module schema is changed.

Explicitly call Admin POST /api/pools/admin/legacy-cleanup in an allowed test environment. It resolves every historical Pools Order, cancels through Orders, verifies terminal Cancelled/Refunded and no Paid/Reserved state, then removes only mock Pools bookings. Nonmock rows, missing financial references or failed settlement stop cleanup. Final empty check clears old catalogue snapshots/mock table. There is no startup data cleanup.

Rollback: disable PurchaseEnabled first, retain catalogue/recovery for outstanding financial work. Preserve new tables and key ring. Application rollback is not a destructive database downgrade.

## Frontend and verification

Both rendering inventories register canonical/legacy routes. Discovery/details are SSR; count selection is a WASM island. Cart/checkout/purchase/operator/admin use WASM prerender=false and existing browser auth/design system. Cart/draft use tab-local sessionStorage; submitted payload/key freezes. Shared Orders Checkout/Profile routes use canonical purchases. Private navigation/PDF/API are network-only/no-store; worker cache generation invalidates old pages. Legacy discovery redirects and old booking links show a controlled message.

Backend tests cover provider contracts, money/time/domain, actual PDF/crypto, isolated PostgreSQL sync/idempotency/payment/refund/consumption/concurrency and HTTP API. Financial integration uses actual Orders/Wallets; doubles exist only in test projects. Set REFAHI_POOLS_TEST_CONNECTION to an isolated database whose name contains test. Frontend Chromium uses a test-only API server at55732 and Development UI host at55733; set POOLS_UI_URL. It covers SSR without JS, mobile layout, single-session checkout, lost result/typed conflict, invalid draft, operator consumption and real published SW.

Local connection is used only to create an isolated test database. Application data is not migrated/seeded by the integration suite. The integration suite uses doubles for upstream operations. Live Local organizational reads and sync are separately verified in the Bruno alignment report; production sales remain gated.

## Operational gate

The placeholder reservation gateway has been removed. New paid organizational purchases use real reserve with a durable no-resend guard and encrypted supplier vouchers. Successful or uncertain external reservations have no online cancellation. Unknown outcomes require operational investigation. `PurchaseEnabled=false` remains the rollout default until a controlled staging purchase confirms reserve financial semantics. See the current Bruno alignment report for Local evidence and the confirmed geography review policy.

## Historical validation result — 2026-10-13

Both full Solutions build successfully (frontend zero warnings/errors; backend existing warnings outside Pools). Pools backend:43 passed, zero skipped, including real isolated PostgreSQL and Chromium PDF. Pools frontend:16 passed, zero skipped, including browser/PWA. Orders13, Wallets10 and Shell24 tests passed. EF reports no pending model changes. No Checkout/Profile standalone test project exists; shared checkout and Profile route registration are covered by browser tests/build. No live PoolTicket call or production activation was performed.
