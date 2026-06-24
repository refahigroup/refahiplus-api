# Codex Implementation Prompt — Phase 19: Backend ShopProductVariant Domain, Persistence, Contracts, and Admin API

Read `.codex/prompts/_session-bootstrap.md` first.
Then read `AGENTS.md`.
Load only the relevant backend/module/store/order architecture skills from `.codex/skills/`.

This is a focused Backend API implementation task.

Run this prompt from:

```text id="085nxd"
C:\Workspace\repo\refahiplus-api
```

Do not modify Admin Web.
Do not modify Customer WebApp.
Do not modify Docker/GitHub Actions.
Do not change cart/checkout pricing behavior in this phase.
Do not change public product detail price resolution in this phase.
Do not remove existing `ShopProduct` behavior.
Do not remove `ProductVariant.PriceMinor`.
Do not add shop-level stock/capacity override.

## Mandatory Report Output

At the end of this task, save the final report as:

```text id="h7zuy2"
.codex/reports/06-phase-19-backend-shopproductvariant-domain-contracts.md
```

If `.codex/reports/` does not exist, create it.

Also print the same report in the Codex final response.

## Context

Phase 18 planning selected Option B:

```text id="qfa7t1"
ShopProduct remains the parent shop/product listing.
ShopProductVariant is introduced as a child offering row for products with variants.
```

Target model:

```text id="pb6uku"
Product without variants:
- ShopProduct remains the sellable listing.
- ShopProduct owns shop-specific price/discount/description/active state.

Product with variants:
- ShopProduct remains the parent listing.
- ShopProductVariant defines which ProductVariants are available in that shop.
- ShopProductVariant owns shop-specific variant price/discount/active state.
```

Important decisions from Phase 18:

* Do not add capacity to `ShopProductVariant` in v1.
* Do not add stock override to `ShopProductVariant` in v1.
* Capacity remains on `ProductVariant`.
* Stock remains on `Product` / `ProductVariant` for StockBased products.
* `ShopProductVariant` is for shop-specific:

  * price
  * discount
  * active/inactive
  * soft delete
* Pricing behavior switch is deferred to a later phase.
* Cart/checkout price resolver is deferred to a later phase.
* Customer WebApp display update is deferred to a later phase.

## Goal

Implement backend support for managing `ShopProductVariant` rows without changing runtime cart/checkout behavior yet.

After this phase:

* Store Domain has a `ShopProductVariant` entity.
* `ShopProduct` owns/manages `ShopProductVariant` rows.
* EF maps `ShopProductVariant` to `store.shop_product_variants`.
* Migration creates the table and indexes.
* Application contracts expose DTOs/commands for managing shop product variants.
* Store Application handlers allow admin-side list/upsert/enable/disable/remove operations.
* Store API exposes admin endpoints under the existing shop-product/admin route style.
* Existing `ShopProduct` endpoints continue working.
* Existing cart/checkout/product detail pricing behavior remains unchanged.

## Repository

Primary repository:

```text id="wi4wr3"
C:\Workspace\repo\refahiplus-api
```

Do not modify:

```text id="c7iv3r"
C:\Workspace\repo\refahiplus-admin
C:\Workspace\repo\refahiplus-webapp
```

## Required Pre-Check

Run:

```bash id="83vfrd"
git status --short
```

Report dirty state clearly.

If the repo is not clean, preserve existing user changes and do not touch unrelated files.

## Required Investigation Before Editing

Inspect actual files and follow the current project style.

Search and inspect:

```text id="2en379"
ShopProduct
ShopProductsEndpoints
AddShopProduct
UpdateShopProduct
RemoveShopProduct
EnableShopProduct
DisableShopProduct
IShopProductRepository
ShopProductRepository
ShopProductConfiguration
StoreDbContext
ProductVariant
ProductVariantConfiguration
Product
PriceMinor
DiscountedPriceMinor
Money
Result
Error
Validation
Endpoint
MapGroup
RequireAuthorization
Admin
```

Likely areas:

```text id="kskrjg"
src/Refahi.Modules.Store.Domain/
src/Refahi.Modules.Store.Application.Contracts/
src/Refahi.Modules.Store.Application/
src/Refahi.Modules.Store.Api/
src/Refahi.Modules.Store.Infrastructure/
src/Refahi.Modules.Store.Infrastructure/Persistence/
src/Refahi.Modules.Store.Infrastructure/Migrations/
```

Do not assume naming. Adapt to current conventions.

## Required Domain Model

### 1. Add ShopProductVariant Entity

Add a Store Domain entity:

```text id="2hwl9a"
ShopProductVariant
```

Recommended fields:

```csharp id="fecsvx"
public Guid Id { get; private set; }
public Guid ShopProductId { get; private set; }
public Guid ProductVariantId { get; private set; }

public long PriceMinor { get; private set; }
public long? DiscountedPriceMinor { get; private set; }

public bool IsActive { get; private set; }
public bool IsDeleted { get; private set; }

public DateTime CreatedAt { get; private set; }
public DateTime UpdatedAt { get; private set; }
```

Use existing project base entity / audit conventions if present.

Important:

* `DiscountedPriceMinor` should be nullable.
* Do not use fake `0` discount values.
* Do not add capacity fields.
* Do not add stock fields.
* Do not add validity override fields.
* Do not add description in v1 unless current domain conventions strongly require it.

### 2. ShopProduct Aggregate Behavior

Update `ShopProduct` to own/manage child variants if this matches current aggregate style.

Add methods such as:

```csharp id="694g09"
AddVariantOffering(...)
UpdateVariantOffering(...)
EnableVariantOffering(...)
DisableVariantOffering(...)
RemoveVariantOffering(...)
```

Adapt names to project style.

Rules:

* One non-deleted offering per `ShopProductId + ProductVariantId`.
* Cannot add duplicate active/non-deleted offering.
* `PriceMinor` must be positive.
* `DiscountedPriceMinor`, if present, must be positive and less than `PriceMinor`.
* Removing should soft-delete if current `ShopProduct` uses soft-delete patterns.
* Updating a deleted offering should not be allowed unless existing style supports restore.
* Enabling/disabling should update `UpdatedAt`.

If the aggregate currently does not load child collections, use repository methods safely, but keep domain invariants centralized as much as possible.

### 3. ProductVariant Validation

When adding/updating a `ShopProductVariant`, validate that:

* `ProductVariantId` belongs to the same `ProductId` as the parent `ShopProduct`.
* The target `ProductVariant` is not deleted/inactive if such flags exist.
* The parent `ShopProduct` is not deleted.
* Prefer not to require parent `ShopProduct.IsActive` for editing offerings, unless existing admin behavior requires it.

Use existing repositories/queries to load product variants as needed.

## Persistence / EF

### 4. Add EF Mapping

Add configuration for `ShopProductVariant`.

Target table:

```text id="clfvvc"
store.shop_product_variants
```

Columns:

```text id="hgl0h8"
Id
ShopProductId
ProductVariantId
PriceMinor
DiscountedPriceMinor
IsActive
IsDeleted
CreatedAt
UpdatedAt
```

Use existing naming conventions from Store migrations.

Indexes:

```text id="tqypv6"
ShopProductId
ProductVariantId
IsDeleted
Filtered unique index on (ShopProductId, ProductVariantId) where IsDeleted = false
```

Add FK:

```text id="nozdrx"
ShopProductVariant.ShopProductId -> ShopProduct.Id
ShopProductVariant.ProductVariantId -> ProductVariant.Id
```

Use delete behavior consistent with current Store mappings. Prefer Restrict/NoAction if the project uses that to avoid cascade surprises.

### 5. Add DbSet and Migration

Update Store DbContext.

Create a migration named similar to:

```text id="2txr50"
Store_AddShopProductVariants
```

Do not apply migration to a real database.

Migration should only add the new table and indexes.
Do not backfill rows in this phase unless the current migration conventions strongly require it.

Backfill is deferred to a later phase.

## Repository / Query Support

### 6. Repository Support

Add repository methods consistent with current style.

Possible methods:

```csharp id="rcr2vd"
Task<ShopProduct?> GetByIdWithVariantsAsync(Guid shopProductId, CancellationToken cancellationToken);
Task<ShopProduct?> GetByShopAndProductWithVariantsAsync(Guid shopId, Guid productId, CancellationToken cancellationToken);
Task<ShopProductVariant?> GetVariantOfferingAsync(Guid shopProductId, Guid productVariantId, CancellationToken cancellationToken);
Task<IReadOnlyList<ShopProductVariant>> ListVariantOfferingsAsync(Guid shopProductId, CancellationToken cancellationToken);
```

Only add methods actually needed by handlers.

Avoid broad generic repository expansion.

## Application Contracts / DTOs

### 7. Add DTOs

Add contracts following current Store Application.Contracts style.

Suggested DTO:

```csharp id="7a6581"
public sealed record ShopProductVariantDto(
    Guid Id,
    Guid ShopProductId,
    Guid ProductVariantId,
    string VariantName,
    long PriceMinor,
    long? DiscountedPriceMinor,
    bool IsActive,
    bool IsDeleted
);
```

If project uses classes instead of records, follow current style.

Useful admin fields may include:

```text id="ydork4"
Variant attributes/combinations summary
ProductVariant base price
ProductVariant base discounted price
ProductVariant capacity metadata
```

But keep DTO minimal unless current admin API pattern benefits from richer summary.

### 8. Add Commands / Queries

Add commands/queries for admin management.

Suggested:

```csharp id="1zso4h"
ListShopProductVariantsQuery(Guid ShopProductId)

UpsertShopProductVariantCommand(
    Guid ShopProductId,
    Guid ProductVariantId,
    long PriceMinor,
    long? DiscountedPriceMinor,
    bool IsActive
)

EnableShopProductVariantCommand(Guid ShopProductId, Guid ProductVariantId)

DisableShopProductVariantCommand(Guid ShopProductId, Guid ProductVariantId)

RemoveShopProductVariantCommand(Guid ShopProductId, Guid ProductVariantId)
```

Alternative path using IDs is acceptable if consistent:

```text id="fhbz2a"
ShopProductVariantId-based enable/disable/remove
```

Choose the least disruptive convention.

Validation:

* IDs required.
* Price positive.
* Discount positive and less than price when provided.
* Parent ShopProduct exists and not deleted.
* ProductVariant belongs to ShopProduct.ProductId.
* Duplicate non-deleted offering rejected or treated as update only if command is named Upsert.

### 9. Handler Behavior

Implement handlers in Store Application.

Expected behavior:

#### List

* Returns all non-deleted variant offerings for a ShopProduct.
* Ideally includes enough variant summary for Admin UI.

#### Upsert

* If non-deleted offering exists: update price/discount/active.
* If not: create new offering.
* Validate variant belongs to product.
* Do not affect `ProductVariant.PriceMinor`.
* Do not affect `ProductVariant.StockCount`.
* Do not affect `ProductVariant.Capacity`.

#### Enable/Disable

* Toggle only `ShopProductVariant.IsActive`.
* Do not toggle parent `ShopProduct`.

#### Remove

* Soft-delete offering.
* Do not delete ProductVariant.
* Do not delete ShopProduct.

## API Endpoints

### 10. Admin Endpoints

Add endpoints in Store API following current `ShopProductsEndpoints` style.

Suggested route shape:

```text id="blljea"
GET    /admin/shops/{shopId}/products/{productId}/variants
PUT    /admin/shops/{shopId}/products/{productId}/variants/{variantId}
POST   /admin/shops/{shopId}/products/{productId}/variants/{variantId}/enable
POST   /admin/shops/{shopId}/products/{productId}/variants/{variantId}/disable
DELETE /admin/shops/{shopId}/products/{productId}/variants/{variantId}
```

or if current API uses `shopProductId`:

```text id="qdwuc8"
GET    /admin/shop-products/{shopProductId}/variants
PUT    /admin/shop-products/{shopProductId}/variants/{variantId}
POST   /admin/shop-products/{shopProductId}/variants/{variantId}/enable
POST   /admin/shop-products/{shopProductId}/variants/{variantId}/disable
DELETE /admin/shop-products/{shopProductId}/variants/{variantId}
```

Choose the route style that best fits current Store API.

Do not change existing endpoints.

Use current authorization/admin policies.

Use existing response/result mapping conventions.

### 11. Request Models

Add request model for upsert:

```csharp id="9fpnev"
public sealed record UpsertShopProductVariantRequest(
    long PriceMinor,
    long? DiscountedPriceMinor,
    bool IsActive
);
```

Use existing API request class style.

## Strict Out of Scope

Do not:

* Modify Admin Web.
* Modify Customer WebApp.
* Change public product detail variant prices.
* Change add-to-cart price resolution.
* Change sync cart price resolution.
* Change checkout price resolution.
* Add `shop_product_variant_id` to order metadata yet.
* Add cart price resolver yet.
* Add backfill migration data rows.
* Remove fallback behavior.
* Remove `ShopProduct`.
* Remove `ProductVariant.PriceMinor`.
* Remove `ProductVariant.StockCount`.
* Add stock/capacity/validity override to `ShopProductVariant`.
* Add remaining-capacity display.
* Change Orders/Wallet.
* Change auth/security.
* Change package versions or target frameworks.
* Mark production-ready.

## Backward Compatibility Requirements

Existing behavior must continue:

* Existing ShopProduct APIs still work.
* Existing ShopProduct price/discount behavior still works.
* Existing product detail still builds.
* Existing cart/checkout tests still pass.
* Existing StockBased behavior remains unchanged.
* Existing SessionBased/ProductSession behavior remains unchanged.
* Existing variant-capacity UsageDate behavior remains unchanged.
* Existing Admin/WebApp clients should not break because old DTO fields/routes remain available.

## Validation

From backend repository root, run:

```bash id="wex6ll"
dotnet restore .\Refahi.Backend.slnx
dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore
dotnet test .\Refahi.Backend.slnx --configuration Release --no-build
```

If there are existing Store tests and it is practical, add focused tests for:

* Upsert creates offering.
* Upsert updates existing offering.
* Duplicate active offering is not created.
* Discount must be less than price.
* Variant must belong to parent product.
* Remove soft-deletes offering.
* Enable/disable toggles active state.

Do not introduce a new test framework.

Do not claim validation passed unless commands actually pass.

## Manual QA

Manual API QA is optional.

If local API and auth are available, test:

```text id="t86c84"
- Add ShopProductVariant for a product variant.
- Update price/discount.
- Disable offering.
- Enable offering.
- Remove offering.
- Try adding variant that belongs to another product and verify rejection.
```

If not possible, report manual QA as not run.

## Final Report Format

Save the report to:

```text id="fpa6v0"
.codex/reports/06-phase-19-backend-shopproductvariant-domain-contracts.md
```

Use exactly this structure:

```text id="5u5eyg"
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

Domain changes:
- ...

Persistence / migration:
- ...

Repository changes:
- ...

Application contracts:
- ...

Application handlers:
- ...

API endpoints:
- ...

Backward compatibility:
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
