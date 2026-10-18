# Promotions: financial architecture and operations

Status: implemented; disabled by default until an operator configures and funds a valid System wallet. Decision approved in this conversation on 2026-10-17.

## Ownership and scope

Promotions owns campaigns, codes, reservations, item discount allocation and campaign audit. Organizations owns organizations, membership validity and membership audit. Orders alone builds trusted pricing context and coordinates Promotions and Wallets. Feature modules have no Promotions reference. Flights/Cinema validate the service amount supplied by Orders while customer payment is net.

Exactly five Promotions projects are registered in Refahi.Backend.slnx. The database schemas are `promotions`, `organizations`, `orders`, `wallets`. Cross-module identifiers have no cross-schema foreign keys.

Customer amount = service amount after intrinsic item discounts + shipping - promotion. Shipping does not increase the eligible discount basis. Positive customer payment is required. One code is allowed per order. Fixed/percentage discounts, caps, campaign/code/user limits and organization/user/module/category targeting are supported. Rule lists intersect; alternatives within each list match any item. Campaign rules are editable only in Draft.

## Financial decision

For a service worth 10,000,000 IRR with 1,000,000 IRR promotion, Orders.FinalAmountMinor and the payment header amount are 9,000,000 IRR. Customer allocations total 9,000,000. A separate allocation row, `is_additional=true`, reserves 1,000,000 from the configured Refahi System wallet. Both sources are held, captured, released and refunded atomically by the existing payment writer. Header amounts and customer-facing allocation responses exclude additional funding.

Additional funding is generic Wallet infrastructure, not a campaign rule or transfer into the user's wallet. `is_additional` is persisted on intent, payment and refund allocation records. Destination credits and existing in-person posting plans retain the full pre-promotion service amount. Reports reconciling the total funding must sum all allocation rows, not just the customer payment header. Funding-wallet balance is a real constraint independent of the campaign budget.

Refund reverses the original customer and additional-funding rows, and the original destination/postings. It never credits the customer's wallet with the promotion amount. The campaign budget and all usage quotas are released after authoritative full refund. Partial refunds are outside this version.

## Configuration and rollout

Use the existing runtime configuration mechanism, for example environment variables:

```text
Promotions__Enabled=false
Promotions__FundingWalletId=<existing-active-IRR-System-wallet-id>
```

Do not copy a real wallet ID into source. Create/fund the wallet through existing authorized wallet administration; do not change balances directly. Confirm that it is type System (1), Active (1), IRR. Campaign budget is not a substitute for this balance.

1. Back up the database and deploy the additive backend migrations using the normal release mechanism. Organizations and Promotions have their own migration histories in the existing EF history mechanism; Orders and Wallets add compatible columns/tables. Existing orders are not repriced.
2. Retain the application's persisted Data Protection key ring across restarts and share it between API replicas. It protects code exports and recoverable operation payloads; losing it can prevent recovery/export.
3. Deploy Webapp and Admin contracts. Admin management uses the existing AdminApiClient, cookie/JWT bridge and MudBlazor.
4. Keep Enabled=false while creating membership/campaign drafts, configuring the wallet and checking `/api/promotions/admin/funding`.
5. Fund the wallet and enable the feature in runtime configuration after the acceptance tests. Pause specific campaigns for targeted shutdown.

Enabled=false stops new preview/application. It does not disable completion/recovery/refund for already-started payments. Do not roll back funding columns or run an older payment writer while sponsored payments exist. Prefer a forward fix; disable new applications while retaining the new workers.

## Public and internal contracts

Authenticated owner operations:

- POST `/api/orders/{id}/promotion/preview`: Code, ExpectedPricingVersion. No reservation.
- PUT `/api/orders/{id}/promotion`: Code, ExpectedPricingVersion, IdempotencyKey.
- DELETE `/api/orders/{id}/promotion`: ExpectedPricingVersion, IdempotencyKey in JSON body.
- POST existing `/api/orders/{id}/pay`: customer Allocations, IdempotencyKey, ExpectedPricingVersion. The version is required after a promotion has ever changed the price. Older clients remain valid for unchanged, non-promoted orders.

The order detail also exposes PromotionsEnabled so the customer input remains hidden when new applications are disabled. Existing discounted orders can still be resumed/removed. Reserved payment options reuse the original customer holds rather than requesting another top-up; a changed wallet allocation requires conflict recovery, not a different capture.

Apply/remove return order ID, pricing version, pre-code amount, discount, net amount, reservation deadline and item discount shares. The browser cannot specify funding wallets or discount amounts. Mutations serialize on the existing order mutation lock. A different code requires removal of the old one. Repeated keys replay the persisted result; a different request with the same key conflicts. No new feature-owned checkout is introduced.

Admin-only prefixes: `/api/promotions/admin` and `/api/organizations/admin`. Campaigns expose list/detail/create/update/active; campaign codes expose generate/list/disable/audited export. Redemptions expose filtered read-only reports. Organizations expose list/create/update/member list/add/revoke. Codes are masked outside explicit exports; exported responses are no-store. User/IP limits apply to code attempts; errors are Persian unified API responses.

`SourceAmountMinor` excludes shipping and promotion; `AmountBeforePromotionMinor` includes shipping. PricingVersion is separate from EF xmin. Paid integration events add service amount and funding amount without changing the existing net amount field.

## Failure/recovery contract

Orders persists an operation before reserving Promotions capacity. Campaign-scoped PostgreSQL advisory locks serialize usage/budget decisions; a filtered unique index prevents concurrent live reservations on one order. Membership is rechecked before beginning payment.

Reservations expire at the earliest of 15 minutes, campaign end and order deadline. Once PaymentPending, time alone is insufficient proof to release capacity. Recovery holds the order lock and queries Wallets by the deterministic reserve key. Captured payments are reflected into Orders before events are delivered; replay does not debit again. Reserved intents must be explicitly released before capacity is freed. An expired held order is cancelled, including its source cancellation participant, rather than reusing a released payment key. Unknown states retain capacity and require recovery.

Orders stores Paid/Cancelled/Refunded integration events in its local outbox transaction. Delivery claims one row with SKIP LOCKED, uses a five-minute lease, retries with exponential delay (maximum five minutes), and dead-letters after ten failures. Processing leases can be reclaimed. Promotion side effects are idempotent under the redemption's campaign lock; terminal Reversed state ignores late Paid events. Outbox also suppresses stale Paid delivery when the authoritative order is no longer Paid. Legacy paid-event payloads remain readable.

PromotionRecoveryWorker scans every 30 seconds for unfinished operations and pending reservations. Its logs include OrderId and redemption/campaign identifiers. No code plaintext is logged.

## Operational checks

Admin campaign list shows occupied campaign budget and System funding-wallet availability/holds. Redemption report filters by order, user and state. Membership creation validates Identity users and rejects overlapping active intervals.

Investigate:

- Low funding balance: replenish using existing wallet procedures; do not silently drop a discount or increase a submitted payment amount.
- PaymentPending longer than expected: inspect Orders and Wallets with the same deterministic reserve key; retry recovery, never manually mark a redemption successful/released.
- DeadLettered delivery: investigate the consumer failure and authoritative order/payment state before an operator repairs/requeues delivery through the established operational process. The UI deliberately has no direct financial-outcome override.
- Missing key ring: restore the original protected keys; generating new keys does not decrypt old code batches or pending operations.

Read-only diagnostics (no repair SQL):

```sql
SELECT status, count(*) FROM orders.outbox_messages GROUP BY status;
SELECT "Status", count(*), sum("DiscountMinor") FROM promotions.redemptions GROUP BY "Status";
SELECT "OrderId", "CreatedAt" FROM orders.promotion_operations WHERE "ResultJson" IS NULL ORDER BY "CreatedAt";
```

## Validation

New Promotions tests include domain rounding/targeting, real PostgreSQL migrations, concurrent last-slot reservations, organization revocation, two-source capture/release/refund, complete destination/in-person posting amounts, recovery after capture before saving Orders, encryption/masking, HTTP authorization/versioning/rate limits and actual durable outbox delivery. Real external provider purchases are not part of automated tests; service-boundary tests cover discounted ticket validation.

Run against a dedicated database whose name contains `_test`:

```powershell
$env:REFAHI_PROMOTIONS_TEST_CONNECTION='<dedicated PostgreSQL test connection>'
$env:REFAHI_WALLETS_TEST_CONNECTION='<separate dedicated Wallet test database connection>'
dotnet test Refahi.Backend.slnx --configuration Release
```

The existing Wallet test resets its own schema, so its database must be separate. Missing Promotions test configuration produces explicit skips. Do not point these variables at production.

Build/test Webapp and build Admin in Release. Verify the customer WASM application with a hard refresh after deployment. The published service worker keeps API requests network-only; promotion secrets never enter URLs/sessionStorage. Rendering inventory records the existing shared checkout and explicitly approved Admin Server routes.

Out of scope: partial refund, first-purchase detection, product targeting, multiple simultaneous codes, supplier/organization-funded campaigns and external membership synchronization.
