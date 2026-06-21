Summary:
- Planning-only report for migrating Store session/access products from `ProductSession` to `ProductVariant` as the sellable option.
- Current backend is partially prepared: `ProductVariant` already has `FromDate`, `ToDate`, `CapacityType`, `Capacity`, `RequiresUsageDate`, and capacity validation methods in Domain, but EF configuration, migrations, contracts, DTO projection, Admin DTOs/UI, customer DTOs/UI, cart, and checkout do not yet carry these fields end to end.
- Existing session flow is still active across Store Domain, Application, API, Infrastructure, Admin UI/client, customer cart models, and checkout metadata. It should be kept temporarily for legacy products, hidden for new variant-based products, then removed only after migration.
- Capacity must not use `ProductVariant.SoldCount`. Runtime sold count should be computed from Orders-owned paid order data through an Orders Application.Contracts query or payment-validation extension point, not by Store directly querying Orders tables.
- First implementation should be treated as low-volume safe only: validate at product detail/add-to-cart/cart sync/order creation and again before payment capture. Atomic high-volume enforcement needs a reservation/ledger or transactional lock design before production scale.

Skills used:
- `.codex/skills/architecture-guardian.md`
- `.codex/skills/order-wallet-guardian.md`
- `.codex/skills/rendering-governance-expert.md`
- `.codex/skills/frontend-component-reuse.md`
- `.codex/skills/design-to-ui-implementation.md`

Repositories inspected:
- `C:\Workspace\repo\refahiplus-api`
- `C:\Workspace\repo\refahiplus-admin`
- `C:\Workspace\repo\refahiplus-webapp` for customer product/cart/checkout behavior because the planning questions explicitly require customer UI impact.
- Required governance docs read from `C:\Workspace\Refahi\docs`: overview, architecture, rendering governance constitution, page rendering inventory, rendering migration report.

Current session dependency map:
- Domain:
  - `src/Refahi.Modules.Store.Domain/Entities/ProductSession.cs` owns legacy session date/time/capacity/sold-count behavior.
  - `src/Refahi.Modules.Store.Domain/Repositories/IProductSessionRepository.cs` exposes session lookup/update methods.
  - `src/Refahi.Modules.Store.Domain/Aggregates/Product.cs` keeps `_sessions`, exposes `Sessions`, and has `AddSession(...)`.
  - `src/Refahi.Modules.Store.Domain/Aggregates/Cart.cs` merges by `ShopId + ProductId + VariantId + SessionId`.
  - `src/Refahi.Modules.Store.Domain/Entities/CartItem.cs` stores nullable `SessionId`.
  - `src/Refahi.Modules.Store.Domain/Enums/SalesModel.cs` contains `SessionBased = 2`.
- Application:
  - Cart handlers depend on `IProductSessionRepository` and `SessionId`: `AddToCart`, `GetCart`, `SyncCart`.
  - Checkout depends on sessions: `PlaceStoreOrderCommandHandler` validates `Product.Sessions`, writes `session_id/date/start_time/end_time` into metadata, and has unused `sessionUpdates`; `StoreOrderPaidEventHandler` reads `session_id` and calls `session.Sell(...)`.
  - Product detail/admin queries include sessions only when `SalesModel.SessionBased`: `GetProductBySlugQueryHandler`, `AdminGetProductQueryHandler`.
  - Session feature handlers: `CreateSession`, `UpdateSession`, `CancelSession`, `GetProductSessions`.
- API:
  - Store session endpoints under `src/Refahi.Modules.Store.Api/Endpoints/Sessions`: create/update/cancel/get product sessions.
  - API responses use `ProductSessionDto` for legacy session lists.
- Infrastructure:
  - `StoreDbContext` exposes `DbSet<ProductSession>` and applies `ProductSessionConfiguration`.
  - `ProductSessionRepository` implements `IProductSessionRepository`.
  - `CartItemConfiguration` maps nullable `SessionId`.
  - Existing migrations and `StoreDbContextModelSnapshot` contain `product_sessions` and cart item `SessionId`.
- Admin Web:
  - `CreateEditProductPage.razor` uses `IsSessionBased`, embeds `ProductSessionEditor`, validates draft sessions before create, maps `_product.Sessions`, and calls create/update/cancel session API methods.
  - `ProductSessionEditor.razor` is a full session editor with date, start/end time, capacity, sold count, remaining capacity, active/cancel state.
  - `AdminApiClient.cs` exposes `CreateProductSessionAsync`, `UpdateProductSessionAsync`, `CancelProductSessionAsync`, `ProductSessionDto`, `CreateProductSessionRequest`, `UpdateProductSessionRequest`.
- Cart/checkout/order:
  - Backend cart contracts include `SessionId`: `AddToCartCommand`, `SyncCartCommand`, `CartItemDto`.
  - Customer webapp local and remote cart models include `SessionId`; anonymous cart merge is currently keyed by product/variant/session only.
  - Orders do not have a first-class session field; Store writes session details into `OrderItem.MetadataJson`.
- DTOs/contracts:
  - Backend Store contracts: `ProductDetailDto.Sessions`, `ProductSessionDto`, session commands/queries, cart DTOs/commands.
  - Customer webapp Store DTOs/view models: `ProductSessionDto`, `ProductSessionServiceItem`, `ProductSessionViewModel`, cart item/session fields.
  - SupplyChain keeps `SalesModel` on `AgreementProduct` in Domain, contracts, API endpoints, validators, EF config, and migrations.

Variant validity/capacity integration points:
- Domain:
  - Already present in `ProductVariant`: `FromDate`, `ToDate`, `CapacityType`, `Capacity`, computed `RequiresUsageDate`, `ValidateOrderEligibility(DateOnly? usageDate)`, and `EnsureCapacityAvailable(...)`.
  - Keep these fields on `ProductVariant`; do not add `SoldCount`.
  - Add/update Product aggregate behavior for changing variant validity/capacity after creation; current inspected surface only has add-variant behavior, no complete update/delete variant backend implementation.
- Application contracts:
  - Extend `ProductVariantDto` with `FromDate`, `ToDate`, `CapacityType`, `Capacity`, `RequiresUsageDate`.
  - Extend `AddProductVariantCommand` and future update variant command with the same fields.
  - Extend cart contracts with `UsageDate` on `AddToCartCommand`, `SyncCartCommand.LocalCartItemInput`, `CartItemDto`.
  - Extend order metadata produced by Store with `variant_id`, `from_date`, `to_date`, `capacity_type`, `usage_date` when applicable.
- API requests/responses:
  - `AddProductVariantEndpoint` body currently accepts the command directly; the command must carry validity/capacity fields.
  - Add/update variant endpoints should validate Persian error messages via FluentValidation.
  - Product detail/admin product responses must expose variant validity/capacity fields.
- EF configuration/migration:
  - `ProductVariantConfiguration` currently does not map `FromDate`, `ToDate`, `CapacityType`, or `Capacity`; add explicit mappings and indexes.
  - Create a migration for `store.product_variants`: nullable date columns, required smallint `capacity_type` default `Unlimited`, nullable `capacity`.
  - Recommended indexes: `(product_id)`, `(product_id, capacity_type)`, and optionally `(product_id, from_date, to_date)` for product detail/admin queries.
- Admin API client:
  - Extend `ProductVariantDto`, `AddProductVariantRequest`, `UpdateProductVariantRequest` with validity/capacity fields.
  - Add a client enum/string mapping for `VariantCapacityType`.
- Admin UI:
  - Hide or de-emphasize `ProductSessionEditor` for new products.
  - Add variant-level inputs in `CreateEditProductPage.razor`: `FromDate`, `ToDate`, `CapacityType`, `Capacity`.
  - Use existing `JalaliDatePicker` and `MoneyInputField`; keep MudBlazor RTL style.
- Customer/public product detail:
  - Backend product detail is SSR and already uses `ProductPurchaseIsland` as a WASM island. Keep that rendering split.
  - Product detail view model and Store DTO mapping must include variant validity/capacity fields.
  - Product detail should display validity period and notices; product purchase island should request usage date only when the selected variant requires it.
- Cart:
  - Add `UsageDate` to backend and customer local/remote cart models.
  - Merge duplicate cart items by `ShopId + ProductId + VariantId + UsageDate`; legacy `SessionId` remains only for old session products during migration.
- Checkout/order:
  - Store checkout must validate selected variant, usage date, and capacity before creating the Order and again before payment capture.
  - OrderItem should either gain `UsageDate` as a first-class nullable snapshot or, minimally, Store must write `usage_date` into `MetadataJson`. Recommendation: keep Orders generic and use `MetadataJson` first, because Orders already treats module-specific item details as snapshots.

Cart/order usage date plan:
- `CartItem` should get nullable `UsageDate`.
- `OrderItem` does not need a first-class `UsageDate` in the first implementation; Store should serialize it into `OrderItem.MetadataJson` with `variant_id`, `usage_date`, `capacity_type`, and validity snapshot. Add a first-class Orders field only if multiple source modules need the same concept.
- Usage date is required when:
  - selected sellable option is `ProductVariant`;
  - `CapacityType == PerEligibleDay`;
  - both `FromDate` and `ToDate` exist;
  - `FromDate != ToDate`.
- Usage date is not required when `FromDate == ToDate`; Store should infer the single possible date and write it to metadata for audit consistency.
- If the variant has no validity range, usage date should not be accepted unless a later explicit rule allows open-ended usage dates.
- Duplicate cart item merge:
  - New variant model: merge by same `ShopId`, `ProductId`, `VariantId`, and same normalized `UsageDate`.
  - Single-day variant (`FromDate == ToDate`): merge with inferred single date.
  - Unlimited/TotalPeriod variants without usage date: merge by same `ShopId`, `ProductId`, `VariantId`, null `UsageDate`.
  - Legacy sessions: keep current merge by `SessionId` until retired.
- Checkout validates capacity by reloading product/variant, validating availability/date range, then asking Orders for sold quantity in the correct scope.
- Checkout computes sold count from paid Orders:
  - TotalPeriod scope: sum quantities of Store order items with matching `variant_id`.
  - PerEligibleDay scope: sum quantities of Store order items with matching `variant_id` and matching `usage_date`.
- Sold states:
  - Count orders with `PaymentState == Paid` and status in active fulfilled lifecycle such as `Confirmed`, `Processing`, `Shipped`, `Delivered`.
  - Exclude `Unpaid`, `Reserved`, `Released`, `Refunded`.
  - Exclude `Cancelled` and `Refunded` statuses.
- Cancelled/refunded orders are excluded because Orders transitions paid cancellations to refund state; capacity query should filter by both payment state and terminal order status.

Capacity enforcement plan:
- `Unlimited`:
  - No capacity sold-count check.
  - Still validate variant availability and usage date/date range rules.
- `TotalPeriod`:
  - `Capacity` applies to total quantity sold for the variant over its full validity range.
  - Sold count query scope: `variant_id`.
  - Usage date not required.
- `PerEligibleDay`:
  - `Capacity` applies per selected date.
  - Sold count query scope: `variant_id + usage_date`.
  - Usage date required only for multi-day validity ranges; single-day validity is inferred.
- Runtime sold count computation:
  - Do not store on `ProductVariant`.
  - Add an Orders-owned query in `Refahi.Modules.Orders.Application.Contracts`, for example `GetOrderItemSoldQuantityQuery(SourceModule, SourceItemId/ProductId, MetadataFilters, CountedStates)`.
  - Store calls this via MediatR/contracts. Store must not reference Orders Infrastructure or query Orders tables directly.
- Authoritative enforcement:
  - Product detail may show hints only.
  - Add-to-cart/cart sync can do advisory validation.
  - Store `PlaceStoreOrder` must validate before creating an Order.
  - Orders payment flow should add a pre-capture validation extension point so Store can revalidate capacity immediately before wallet capture without Orders referencing Store directly. Recommended shape: interface/contract in Orders Application.Contracts, implemented by Store Application and discovered by DI based on `SourceModule`.
- Atomic concurrency:
  - Must happen around the final pre-capture capacity decision.
  - Robust design: Store-owned capacity reservation/ledger keyed by `variant_id` and optional `usage_date`, created before capture, finalized on `OrderPaidEvent`, released on capture failure/cancel/timeout.
  - Alternative for first low-volume implementation: pre-capture sold-count recheck plus existing optimistic concurrency patterns, with clear TODO that concurrent pay attempts may oversell.
- Acceptable first implementation:
  - No `SoldCount` on variant.
  - Validate in add-to-cart, cart sync, place order, and pre-payment validator.
  - Compute sold count from Orders paid data.
  - Mark high-volume atomic enforcement and reservation expiry as TODO, not production-ready.

Performance/concurrency plan:
- Avoid sold-count queries in product listing; product list cards should use static availability and price only.
- Product detail can include validity and capacity labels, but only include availability hints if a cheap query exists. Do not query per-variant sold counts for every list card.
- Checkout/pre-payment validation must perform authoritative sold-count checks for only cart/order variants.
- Add indexes for JSON metadata only if using JSONB filters in Orders; better long-term option is an Orders-owned projection table for source item sales dimensions (`source_module`, `source_item_id`, `variant_id`, `usage_date`, `quantity`, `payment_state`, `status`).
- Future hardening options:
  - Orders projection/cache maintained from OrderPaid/Refunded/Cancelled events.
  - Store capacity reservation table to prevent concurrent oversell before wallet capture.
  - Redis cache for product detail availability hints, invalidated by order/payment events.
  - PostgreSQL advisory locks per `variant_id` or `variant_id + usage_date` during final reservation, with strict timeout and observability.

Admin UI plan:
- Keep `SalesModel.SessionBased` as a UI/business classification from SupplyChain agreement product.
- Stop requiring `ProductSessionEditor` for new `SessionBased` products once variant fields are available.
- Add variant fields to the variant editor area:
  - `FromDate`
  - `ToDate`
  - `CapacityType` with values `Unlimited`, `TotalPeriod`, `PerEligibleDay`
  - `Capacity`, required only for non-unlimited capacity types.
- Use existing shared controls:
  - `JalaliDatePicker` for dates.
  - Existing MudBlazor form patterns in `CreateEditProductPage.razor`.
  - `MoneyInputField` where price input is involved.
- Warnings/notices:
  - I found no explicit product warning/notice fields in Store or SupplyChain; only `Description` and cart sync warnings exist.
  - Short-term: use product or shop-product description to communicate usage limits.
  - Better: add explicit Store-owned product notice/warning fields if the business requires structured notices on product detail.
- Provider examples:
  - `آقایان + روزهای زوج`: create a variant caption/attribute value that says this, set date range/capacity, and put limitation text in notice/description. System does not enforce gender/calendar parity in v1.
  - `خانم‌ها + روزهای فرد`: same pattern.
  - `تعطیل`: do not model holidays as excluded dates in v1; communicate in notice/description or temporarily make affected variants unavailable. Excluded dates can be added later.
- For `PerEligibleDay`, Admin UI must show a warning that customers will be asked to choose usage date when the validity range spans multiple days.

Customer UI plan:
- Product detail remains SSR. Variant selection, usage date, and add-to-cart remain inside `ProductPurchaseIsland` as a WASM island with `InteractiveWebAssemblyRenderMode(prerender: false)`.
- Show each variant’s validity period beside the option label or in a compact detail block.
- Show `CapacityType` as user-friendly availability wording only if reliable; avoid precise remaining count unless checkout/pre-payment logic can guarantee it.
- Ask for usage date only when the selected variant requires it.
- Do not ask usage date when `FromDate == ToDate`; display the single date as the usage date.
- Display product warnings/notices clearly near purchase controls. If no dedicated fields exist, use existing description until structured notices are added.
- Cart row should display usage date for variant access products.
- Cart and checkout pages are WASM pages and must continue using `@(new InteractiveWebAssemblyRenderMode(prerender: false))`.

Legacy ProductSession strategy:
- Safest phased strategy: keep `ProductSession` temporarily for existing products and existing Admin data, but hide it for new variant-based products.
- Do not delete `ProductSession` or session endpoints in the first migration.
- Add a feature flag or UI condition that routes new `SessionBased` agreement products to variant validity/capacity editor instead of `ProductSessionEditor`.
- Migrate existing session products by creating one `ProductVariant` per active `ProductSession`, preserving title/date/time/capacity/price adjustment as variant caption/attributes and validity metadata. Keep old session records read-only until verification.
- After all customer/admin/cart/order flows use variants and old data is migrated, deprecate session API endpoints, remove Admin client methods, remove session DTOs, then remove Domain/EF mappings in a later migration.

Recommended implementation phases:
- Phase A: Persistence/contracts for ProductVariant validity/capacity
  - Goal: make existing variant Domain fields persist and flow through backend contracts.
  - Files likely to change: `ProductVariantConfiguration.cs`, Store migration/snapshot, `ProductVariantDto.cs`, `AddProductVariantCommand.cs`, add update variant command/handler if missing, product detail/admin query handlers, Admin/client DTO mirrors.
  - Validation commands: `dotnet build Refahi.Backend.slnx`, targeted Store tests if present, `dotnet test`.
  - Risks: existing `ProductVariant` Domain fields currently are not mapped by EF or projected by DTOs.
- Phase B: Admin variant UI
  - Goal: let admins define session/access sellable variants without `ProductSession`.
  - Files likely to change: `CreateEditProductPage.razor`, variant editor section, `AdminApiClient.cs`, possible shared helper for capacity labels.
  - Validation commands: admin `dotnet build`, manual create/edit product test, RTL form validation.
  - Risks: current Admin page still requires sessions for `SessionBased`; must not break legacy products.
- Phase C: Customer product detail/cart usage date
  - Goal: show validity, require usage date only when needed, and carry `UsageDate` through local/remote cart.
  - Files likely to change: webapp Store DTOs, `ProductDetailViewModel.cs`, `ProductService.cs`, `ProductPurchaseIsland.razor`, `ProductVariantSelector.razor`, `ICartService.cs`, `LocalCartService.cs`, `RemoteCartService.cs`, cart view models/components.
  - Validation commands: webapp `dotnet build`, product detail smoke test, anonymous-to-auth cart sync test.
  - Risks: local cart merge/idempotency key must include `UsageDate`; otherwise different days merge incorrectly.
- Phase D: Checkout/order capacity enforcement
  - Goal: enforce capacity from Orders paid data and keep Wallet paying Orders only.
  - Files likely to change: Store cart handlers, `PlaceStoreOrderCommandHandler`, Store paid event handler, Orders Application.Contracts query or pre-payment validator interface, Orders PayOrder handler, Orders query infrastructure/projection.
  - Validation commands: backend `dotnet build`, `dotnet test`, manual pay/cancel/refund capacity scenarios.
  - Risks: without atomic reservation, concurrent payments can oversell; must be documented as TODO until hardened.
- Phase E: Remove/deprecate old ProductSession flows
  - Goal: freeze legacy session creation, migrate data, then remove old surface.
  - Files likely to change: Store session endpoints/features/contracts, Admin `ProductSessionEditor`, Admin API client session methods, Store Domain/Infrastructure session mappings, customer session DTOs.
  - Validation commands: backend/admin/webapp builds, migration dry run, regression test existing migrated products.
  - Risks: deleting too early breaks existing session products and admin edit pages.
- Phase F: Performance/concurrency hardening
  - Goal: make capacity reliable under load.
  - Files likely to change: Orders projection/query service, Store reservation/ledger table, payment validator, background cleanup for expired reservations, observability logging.
  - Validation commands: concurrency tests, load test around simultaneous checkout, cancellation/refund tests.
  - Risks: holding database locks across wallet calls is dangerous; prefer reservation/ledger with expiry over long locks.

Risks / assumptions:
- `ProductVariant` Domain fields already exist but appear not persisted or exposed, so migrations/contracts are still required.
- No dedicated product warning/notice fields were found; using description is a fallback, not a structured product-notice model.
- Current Store checkout comments say stock/capacity decreases after payment, but `PlaceStoreOrderCommandHandler` does not populate `stockUpdates` and final changes actually happen in `StoreOrderPaidEventHandler`; this should be cleaned up during implementation.
- Orders `MetadataJson` is the pragmatic first place for `usage_date`, but JSON filtering needs careful indexing/projection before high volume.
- The Admin repository is outside the API workspace roots but was readable for inspection.
- This report does not mark the migration production-ready; concurrency and performance hardening remain explicit work.

Next implementation prompt:
- Implement Phase A only: persist and expose `ProductVariant` validity/capacity fields end to end in backend Store and Admin API contracts. Do not change cart, checkout, customer UI, or remove ProductSession. Add EF migration, update DTOs/commands/query projections, add validators, and run backend build/tests.
