# Codex Implementation Prompt — Phase 16: Backend Cart/Order UsageDate and Variant Capacity Enforcement

Read `.codex/prompts/_session-bootstrap.md` first.
Then read `AGENTS.md`.
Load only the relevant backend/module/order skills from `.codex/skills/`.

This is a focused Backend API implementation task.

Do not modify Admin Web.
Do not modify Customer WebApp.
Do not modify Docker/GitHub Actions.
Do not remove `ProductVariant.StockCount`.
Do not remove `ProductSession`.
Do not make release decisions.

## Mandatory Report Output

At the end of this task, save the final report as:

```text
.codex/reports/phase-16-backend-cart-order-usagedate-capacity-enforcement.md
```

Also print the same report in the Codex final response.

## Context

The Store module is migrating session/access-style products away from legacy `ProductSession` and toward variant-based sellable options.

Completed phases:

* Phase 7: Added `ProductVariant` validity/capacity fields in Domain.
* Phase 8: Created cross-layer migration plan.
* Phase 9: Persisted/exposed variant validity/capacity fields through backend persistence/contracts.
* Phase 10: Added Admin UI support for variant validity/capacity.
* Phase 11: Analyzed `ProductVariant.StockCount` and `ShopProduct` impact.
* Phase 12: Hardened backend behavior by `SalesModel`.
* Phase 13: Improved Admin UI/UX for StockBased vs SessionBased variants.
* Phase 15: Prepared Customer WebApp product detail/cart models for variant validity/capacity and `UsageDate`, but guarded add-to-cart because backend support is not ready yet.

Current backend behavior after Phase 12:

```text
StockBased:
- Product.StockCount / ProductVariant.StockCount remain authoritative.
- Existing cart/checkout/payment stock behavior must remain working.

SessionBased legacy:
- ProductSession and SessionId remain supported.

SessionBased variant-capacity:
- ProductVariant.CapacityType / Capacity are the semantic capacity fields.
- Current backend blocks unsupported SessionBased VariantId cart/order paths until UsageDate/capacity enforcement is implemented.
```

This phase must add backend support for `UsageDate` and enforce variant capacity for SessionBased variant-capacity products.

## Goal

Enable backend Store cart/order contracts to carry `UsageDate` and enforce `ProductVariant` capacity for SessionBased variant-capacity products.

After this phase:

* Backend AddToCart accepts optional `UsageDate`.
* Backend SyncCart accepts optional `UsageDate`.
* CartItem stores optional `UsageDate`.
* CartItemDto exposes optional `UsageDate`.
* Cart item merge logic includes `UsageDate` where relevant.
* Store order creation writes `usage_date` into `OrderItem.MetadataJson` for variant-capacity items.
* Store validates `ProductVariant.ValidateOrderEligibility(...)`.
* Store validates `ProductVariant.EnsureCapacityAvailable(...)` using runtime sold count.
* StockBased product and variant stock behavior remains unchanged.
* Legacy ProductSession behavior remains unchanged.
* High-volume atomic concurrency/reservation remains deferred with explicit TODOs.

## Repository

Primary repository:

```text
C:\Workspace\repo\refahiplus-api
```

Do not modify Admin or Customer WebApp repositories in this phase.

## Required Pre-Check

Run:

```bash
git status --short
```

Report dirty state clearly.
If there are existing user changes, preserve them and do not touch unrelated files.

## Required Investigation Before Editing

Inspect actual files and adapt to existing structure.

Likely areas:

```text
src/Refahi.Modules.Store.Domain/Aggregates/Cart.cs
src/Refahi.Modules.Store.Domain/Entities/CartItem.cs
src/Refahi.Modules.Store.Domain/Entities/ProductVariant.cs
src/Refahi.Modules.Store.Domain/Aggregates/Product.cs

src/Refahi.Modules.Store.Application.Contracts/Commands/Cart/AddToCartCommand.cs
src/Refahi.Modules.Store.Application.Contracts/Commands/Cart/SyncCartCommand.cs
src/Refahi.Modules.Store.Application.Contracts/Dtos/Cart/CartItemDto.cs

src/Refahi.Modules.Store.Application/Features/Cart/AddToCart/AddToCartCommandHandler.cs
src/Refahi.Modules.Store.Application/Features/Cart/SyncCart/SyncCartCommandHandler.cs
src/Refahi.Modules.Store.Application/Features/Cart/GetCart/GetCartQueryHandler.cs

src/Refahi.Modules.Store.Application/Features/Checkout/PlaceStoreOrder/PlaceStoreOrderCommandHandler.cs
src/Refahi.Modules.Store.Application/Features/Checkout/FinalizeStoreOrder/StoreOrderPaidEventHandler.cs

src/Refahi.Modules.Store.Infrastructure/Persistence/Configurations/CartItemConfiguration.cs
src/Refahi.Modules.Store.Infrastructure/Migrations/
src/Refahi.Modules.Store.Infrastructure/Repositories/

src/Refahi.Modules.Orders.Application.Contracts/
src/Refahi.Modules.Orders.Application/
src/Refahi.Modules.Orders.Infrastructure/
```

Search for:

```text
UsageDate
SessionId
VariantId
ProductVariant
CapacityType
RequiresUsageDate
ValidateOrderEligibility
EnsureCapacityAvailable
MetadataJson
variant_id
session_id
sales_model
OrderItem
PaymentState
Refunded
Cancelled
StoreOrderPaidEventHandler
```

## Required Implementation Areas

### 1. Add UsageDate to Store Cart Contracts

Add nullable `UsageDate` to backend cart contracts:

```csharp
DateOnly? UsageDate
```

Expected contracts to update if present:

```text
AddToCartCommand
SyncCartCommand.LocalCartItemInput
CartItemDto
```

Rules:

* Existing clients that omit `UsageDate` must continue working.
* StockBased products should normally use `UsageDate = null`.
* Legacy ProductSession products should continue using `SessionId`.
* SessionBased variant-capacity products may require `UsageDate`.

Do not remove `SessionId`.

### 2. Add UsageDate to Cart Domain and Persistence

Add nullable `UsageDate` to `CartItem`.

Update cart add/merge behavior:

* Existing merge behavior for StockBased items remains unchanged.
* Existing merge behavior for legacy SessionId items remains unchanged.
* For variant-capacity items, duplicate cart items must merge only when:

  * same `ShopId`
  * same `ProductId`
  * same `VariantId`
  * same normalized `UsageDate`
  * same `SessionId` null

If `FromDate == ToDate`, normalize/infer `UsageDate` to that date before storing.

Add EF mapping and migration for the nullable cart item usage date.

Suggested column name should follow existing Store naming conventions.
Do not apply migration to a real database.

### 3. Add UsageDate Normalization Helper

Implement a small helper in Store Application if useful.

Expected behavior:

```text
If variant.RequiresUsageDate is true:
  UsageDate is required.

If variant.FromDate == variant.ToDate:
  UsageDate may be null from client.
  Backend normalizes UsageDate to FromDate.

If variant has validity range and UsageDate is provided:
  UsageDate must be within FromDate and ToDate.

If variant has no validity range:
  UsageDate should not be accepted unless existing domain rules allow it.
```

Use existing `ProductVariant.ValidateOrderEligibility(DateOnly? usageDate)` where possible.

Do not implement weekday/even-day/holiday/excluded-date logic.

### 4. Add Runtime Sold Count Query Through Orders Contracts

Do not make Store directly query Orders tables.

If an Orders-owned contract already exists for sold quantity, use it.

If no suitable contract exists, add a narrow Orders Application.Contracts query for Store to ask for sold quantity in a module-safe way.

Suggested shape:

```csharp
public sealed record GetStoreVariantSoldQuantityQuery(
    Guid VariantId,
    DateOnly? UsageDate,
    VariantCapacityType CapacityType,
    Guid? ExcludeOrderId = null
) : IRequest<int>;
```

Adapt naming and shape to existing conventions.

The query must count successful sold quantities from Orders data.

Sold count scope:

```text
CapacityType.TotalPeriod:
- count successful sold quantity for matching variant_id.
- usage_date filter is not required.

CapacityType.PerEligibleDay:
- count successful sold quantity for matching variant_id and matching usage_date.
```

Sold states:

* Count only paid/successful/non-cancelled/non-refunded order items.
* Exclude unpaid/reserved/released/refunded/cancelled states.
* Follow existing Orders status/payment state names.
* If exact lifecycle is unclear, inspect and choose the safest existing paid/finalized states, then report assumptions.

Important:

* If validating from a paid event handler after the current order is already paid, exclude the current order/item from the sold-count query to avoid double-counting current quantity.
* Do not change Wallet behavior.
* Do not make Orders reference Store Infrastructure.
* Keep module boundaries clean.

### 5. Capacity Enforcement in AddToCart / SyncCart / GetCart

For `SalesModel.StockBased`:

* Preserve all existing stock validation and availability logic.

For legacy `SalesModel.SessionBased` with `SessionId`:

* Preserve existing ProductSession validation.

For `SalesModel.SessionBased` with `VariantId` and no `SessionId`:

* Allow only if the selected variant is a valid variant-capacity/access option.
* Validate `UsageDate` requirement and range.
* Query runtime sold count in the correct scope.
* Call `variant.EnsureCapacityAvailable(quantity, soldCountInScope)`.
* Add or keep the cart item with normalized `UsageDate`.

Cart sync behavior:

* If capacity is insufficient, either drop the item with a clear warning or clamp quantity to remaining capacity, following existing sync style.
* Do not silently convert capacity to stock.
* Do not use `ProductVariant.StockCount` for SessionBased capacity.

GetCart behavior:

* Include `UsageDate` in returned DTO.
* For cart item availability:

  * StockBased: keep stock-based logic.
  * Legacy SessionBased: keep session-based logic.
  * Variant-capacity: validate current usage date/capacity using runtime sold count if reasonably scoped to cart items.
* Do not expose precise remaining capacity unless it is already computed reliably for that item.

### 6. Capacity Enforcement in PlaceStoreOrder

Update Store order creation.

For `SalesModel.StockBased`:

* Preserve product/variant stock validation.
* Preserve metadata behavior.

For legacy `SalesModel.SessionBased` with `SessionId`:

* Preserve ProductSession validation and metadata.

For `SalesModel.SessionBased` with `VariantId` and no `SessionId`:

* Validate selected variant.
* Normalize/validate `UsageDate`.
* Query sold count in the correct scope.
* Call `EnsureCapacityAvailable`.
* Write metadata snapshot:

```json
{
  "sales_model": "...",
  "variant_id": "...",
  "usage_date": "yyyy-MM-dd",
  "capacity_type": "...",
  "from_date": "yyyy-MM-dd",
  "to_date": "yyyy-MM-dd"
}
```

Use existing metadata naming conventions where possible.

Do not write fake `session_id`.

Do not use `StockCount` as capacity.

### 7. Paid Event Handler Compatibility

Update `StoreOrderPaidEventHandler`.

Expected behavior:

* StockBased non-variant: still decrement Product.StockCount.
* StockBased variant: still decrement ProductVariant.StockCount.
* Legacy SessionBased with `session_id`: still call ProductSession.Sell.
* SessionBased variant-capacity with `variant_id` and `usage_date`:

  * Do not decrement ProductVariant.StockCount.
  * Validate capacity defensively using Orders sold count excluding the current order if possible.
  * Do not store SoldCount on ProductVariant.
  * If capacity is insufficient at paid-event time, fail clearly and report the risk in the final report. Do not silently oversell.
  * If existing architecture cannot safely reject after payment capture, add TODO and report that final atomic enforcement needs reservation/pre-capture phase.

Important:

This phase can provide low-volume safety but not full high-volume atomic safety.

Add concise TODO:

```text
TODO: Replace sold-count recheck with atomic reservation/ledger before high-volume capacity sales.
```

### 8. Error Messages

Use clear Persian error messages and stable error codes consistent with current Store style.

Suggested meanings:

```text
تاریخ استفاده برای این خدمت الزامی است.
تاریخ استفاده خارج از بازه اعتبار خدمت است.
ظرفیت این خدمت برای تاریخ انتخاب‌شده تکمیل شده است.
ظرفیت این خدمت تکمیل شده است.
خرید این خدمت با تنظیمات فعلی امکان‌پذیر نیست.
```

### 9. Backward Compatibility

Preserve:

* Existing StockBased product add-to-cart.
* Existing StockBased variant add-to-cart.
* Existing legacy ProductSession add-to-cart.
* Existing cart sync for old clients.
* Existing checkout/payment behavior for stock products.
* Existing ProductSession paid finalization.
* Existing Admin APIs.
* Existing API route paths.
* Existing order/wallet behavior.
* Existing historical order metadata handling.

### 10. Strict Out of Scope

Do not:

* Modify Admin Web.
* Modify Customer WebApp.
* Remove the WebApp add-to-cart guard.
* Remove `ProductVariant.StockCount`.
* Remove `ProductSession`.
* Remove `SessionId`.
* Add holiday/calendar/even-day/odd-day logic.
* Add exclude dates.
* Implement full reservation/ledger.
* Implement high-volume atomic concurrency solution.
* Add product detail remaining-capacity display.
* Change Wallet payment rules.
* Query Orders tables directly from Store Infrastructure.
* Change package versions or target frameworks.
* Mark production-ready.

## Validation

From Backend API repository root, run:

```bash
dotnet restore .\Refahi.Backend.slnx
dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore
dotnet test .\Refahi.Backend.slnx --configuration Release --no-build
```

If Store-specific tests exist or can be safely added, prefer adding focused tests for:

* UsageDate required when required.
* UsageDate inferred for single-day variant.
* StockBased variant stock behavior still works.
* Legacy ProductSession behavior still works.
* Capacity insufficient rejection.

Do not add a large new test framework if none exists.

Do not claim validation passed unless commands actually pass.

## Manual QA

Manual QA is optional in this backend phase.

If local/stage API environment and data are available, test:

```text
StockBased product:
- add to cart
- checkout
- paid event stock decrement

Legacy SessionBased product:
- add to cart with SessionId
- checkout
- paid event ProductSession.Sell

SessionBased variant-capacity product:
- add to cart with VariantId and UsageDate
- add to cart missing UsageDate when required
- single-day variant without UsageDate
- insufficient capacity
```

If not available, report manual QA as not run.

## Final Report Format

Save the report to:

```text
.codex/reports/phase-16-backend-cart-order-usagedate-capacity-enforcement.md
```

Use exactly this structure:

```text
Summary:
- ...

Skills used:
- ...

Repository checked:
- ...

Git state:
- Pre-check:
- Final:

Files changed:
- ...

Cart contract/domain/persistence changes:
- ...

UsageDate normalization and validation:
- ...

Orders sold-count query:
- ...

Capacity enforcement:
- ...

StockBased behavior preserved:
- ...

Legacy ProductSession behavior preserved:
- ...

Order metadata / paid event changes:
- ...

Migration:
- ...

Validation:
- Command: ...
  Result: ...

Manual QA:
- ...

Deferred items:
- ...

Risks / assumptions:
- ...

Suggested next phase:
- ...
```
