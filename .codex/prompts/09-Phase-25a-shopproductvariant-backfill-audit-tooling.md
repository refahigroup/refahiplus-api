# Codex Implementation Prompt — Phase 25A: ShopProductVariant Backfill and Audit Tooling

Read `.codex/prompts/_session-bootstrap.md` first.
Then read `AGENTS.md`.
Load only the relevant backend/module/store architecture skills from `.codex/skills/`.

This is a focused Backend API implementation task.

Run this prompt from:

```text id="tjryy7"
C:\Workspace\repo\refahiplus-api
```

Do not modify Admin Web.
Do not modify Customer WebApp.
Do not modify Docker/GitHub Actions.
Do not remove runtime fallback behavior.
Do not remove `ProductVariant.PriceMinor`.
Do not remove `ShopProduct.Price`.
Do not change cart/checkout pricing behavior except for safe reporting/audit metadata if needed.
Do not apply migrations to a real database.
Do not make production release decisions.

## Mandatory Report Output

At the end of this task, save the final report as:

```text id="pga24c"
.codex/reports/phase-25a-shopproductvariant-backfill-audit-tooling.md
```

If `.codex/reports/` does not exist, create it.

Also print the same report in the Codex final response.

## Context

The Store pricing refactor has reached this state:

* `ShopProductVariant` exists in backend domain, persistence, contracts, and admin endpoints.
* Admin Web can manage shop-specific variant offerings.
* Backend runtime price resolver uses:

  * `ShopProduct` for non-variant products;
  * active `ShopProductVariant` for variant products;
  * temporary fallback to `ProductVariant` when no non-deleted `ShopProductVariant` exists.
* Customer WebApp consumes shop-specific variant price fields.
* Customer WebApp now passes explicit `shopSlug` to product detail API on shop-specific product detail routes.

The next required step is data readiness:

```text id="zv7jbd"
Existing ShopProduct rows whose Product has variants need corresponding ShopProductVariant rows.
```

Current migration/fallback strategy:

```text id="4gq2xx"
- Do not remove fallback yet.
- First add audit/backfill tooling.
- Backfill missing ShopProductVariant rows from existing ProductVariant base prices.
- Validate data.
- Then in a later phase remove fallback and reject missing shop variant offerings.
```

## Goal

Add backend audit and controlled backfill tooling for `ShopProductVariant`.

After this phase:

* Admin/backend can audit `ShopProduct` rows with variant products and detect missing `ShopProductVariant` offerings.
* Admin/backend can run a controlled backfill that creates missing `ShopProductVariant` rows.
* Backfill uses `ProductVariant.PriceMinor` and `ProductVariant.DiscountedPriceMinor` as initial shop-specific variant prices.
* Existing configured `ShopProductVariant` rows are never overwritten.
* Products without variants are ignored.
* Deleted/inactive products or deleted shop products are handled safely according to current Store rules.
* Runtime fallback remains active.
* No Admin Web or Customer WebApp source is changed.

## Repository

Primary repository:

```text id="zcmx0m"
C:\Workspace\repo\refahiplus-api
```

Do not modify:

```text id="xqrbpb"
C:\Workspace\repo\refahiplus-admin
C:\Workspace\repo\refahiplus-webapp
```

## Required Pre-Check

Run:

```bash id="e8yi5o"
git status --short
```

Report dirty state clearly.

If the repo is not clean, preserve existing user changes and do not touch unrelated files.

## Required Investigation Before Editing

Inspect current implementation and follow existing conventions.

Search for:

```text id="lprewi"
ShopProduct
ShopProductVariant
ProductVariant
ShopProductRepository
IShopProductRepository
StoreDbContext
ShopProductsEndpoints
AdminOnly
ApiResponseHelper
ProductVariant.PriceMinor
DiscountedPriceMinor
IsActive
IsDeleted
GetWithVariantOfferingsAsync
StoreProductPriceResolver
```

Likely areas:

```text id="mzalqq"
src/Refahi.Modules.Store.Domain/
src/Refahi.Modules.Store.Application.Contracts/
src/Refahi.Modules.Store.Application/
src/Refahi.Modules.Store.Api/
src/Refahi.Modules.Store.Infrastructure/
src/Refahi.Modules.Store.Infrastructure/Persistence/
```

Do not assume file names. Use actual project structure.

## Required Design

Implement this as **controlled tooling**, not an automatic migration backfill.

Preferred shape:

```text id="qb0q3l"
1. Audit query:
   tells admin how many ShopProductVariant rows are missing.

2. Backfill command:
   creates missing ShopProductVariant rows using ProductVariant base prices.

3. Optional dry-run:
   returns what would be created without writing.
```

Do not automatically backfill in EF migration unless the project already has a strong convention for data migrations.
Schema already exists from Phase 19, so no schema migration should be needed unless a small support field is absolutely required.

## Required Implementation Areas

### 1. Audit DTO / Query Contract

Add application contract for audit.

Suggested query:

```csharp id="p2kfxn"
public sealed record GetShopProductVariantBackfillAuditQuery(
    Guid? ShopId = null,
    Guid? ProductId = null
) : IRequest<ShopProductVariantBackfillAuditDto>;
```

Suggested DTOs:

```csharp id="kqmkdu"
public sealed record ShopProductVariantBackfillAuditDto(
    int ShopProductsChecked,
    int ProductsWithVariants,
    int ExistingOfferings,
    int MissingOfferings,
    IReadOnlyList<ShopProductVariantBackfillAuditItemDto> Items
);

public sealed record ShopProductVariantBackfillAuditItemDto(
    Guid ShopId,
    string ShopName,
    Guid ProductId,
    string ProductName,
    Guid ShopProductId,
    int VariantCount,
    int ExistingOfferingCount,
    int MissingOfferingCount
);
```

Adapt to project style.

Rules:

* Optional `ShopId` and `ProductId` filters are useful but not mandatory if they complicate implementation.
* Include enough data for QA.
* Avoid returning huge lists without a reasonable cap/paging if the project has pagination conventions.
* At minimum, return counts plus first N details.

### 2. Backfill Command Contract

Add command:

```csharp id="0ezcgj"
public sealed record BackfillShopProductVariantsCommand(
    Guid? ShopId = null,
    Guid? ProductId = null,
    bool DryRun = true
) : IRequest<ShopProductVariantBackfillResultDto>;
```

Suggested result DTO:

```csharp id="ci6hcy"
public sealed record ShopProductVariantBackfillResultDto(
    bool DryRun,
    int ShopProductsChecked,
    int ProductsWithVariants,
    int CreatedOfferings,
    int SkippedExistingOfferings,
    int SkippedInvalidVariants,
    IReadOnlyList<ShopProductVariantBackfillCreatedItemDto> CreatedItems,
    IReadOnlyList<string> Warnings
);
```

Rules:

* Default `DryRun = true`.
* Actual write requires `DryRun = false`.
* Existing configured offerings must not be overwritten.
* Deleted offerings:

  * If a deleted offering exists and no non-deleted offering exists, treat it as missing and create a new non-deleted row, unless domain unique constraints prevent this.
* Inactive non-deleted offerings:

  * Treat as existing and do not overwrite.
* Missing offering:

  * Create new active offering by default.
  * Price from `ProductVariant.PriceMinor`.
  * Discount from `ProductVariant.DiscountedPriceMinor`.
* If `ProductVariant.PriceMinor <= 0`, skip and warn.
* If discounted price is invalid, either clear discount or skip with warning according to domain rules; prefer skip with clear warning if uncertain.
* Products without variants are ignored.

### 3. Application Handler Logic

Implement handlers in Store Application.

Expected algorithm:

```text id="41y564"
For each non-deleted ShopProduct matching optional filters:
  Load Product with variants.
  If product has no variants:
    continue.
  Load existing non-deleted ShopProductVariant offerings.
  For each ProductVariant belonging to Product:
    If offering exists:
      skip.
    Else:
      if variant price is valid:
        in dry-run: add to CreatedItems preview.
        in write mode: create ShopProductVariant with base variant price.
      else:
        skip and warn.
```

Important:

* Do not change `ProductVariant.PriceMinor`.
* Do not change `ShopProduct.Price`.
* Do not change stock/capacity.
* Do not activate/deactivate `ShopProduct`.
* Do not modify cart/order data.

### 4. Domain Method Reuse

Use existing `ShopProduct` aggregate methods from Phase 19 to create offerings when writing.

Do not bypass domain validation by inserting raw entities unless unavoidable.

### 5. Repository / Persistence

Use existing repositories where possible.

If needed, add narrow repository methods for:

```csharp id="baxvaz"
ListShopProductsWithProductsAndVariantOfferingsAsync(...)
```

or equivalent.

Avoid N+1 queries if reasonably possible, but keep implementation clear and safe.

This phase is operational tooling; performance should be acceptable for admin backfill, but report if the implementation is not suitable for very large datasets.

### 6. API Endpoints

Add admin-only endpoints.

Suggested route group under Store admin/shop products:

```text id="tc4fhn"
GET  /admin/shop-product-variants/backfill/audit
POST /admin/shop-product-variants/backfill
```

or use current endpoint conventions.

Query/body examples:

```http id="nv78vs"
GET /admin/shop-product-variants/backfill/audit?shopId={shopId}&productId={productId}

POST /admin/shop-product-variants/backfill
{
  "shopId": null,
  "productId": null,
  "dryRun": true
}
```

Rules:

* Use `AdminOnly`.
* Use existing response wrapper conventions.
* Do not expose as public/customer API.
* Make dry-run the safe default.

### 7. Safety / Idempotency

Backfill must be idempotent.

Running it multiple times should:

```text id="vve04l"
- create missing rows on first write run;
- create zero rows on subsequent write runs;
- never duplicate non-deleted rows;
- never overwrite configured prices.
```

### 8. Backward Compatibility

Preserve:

* Existing ShopProductVariant admin endpoints.
* Existing runtime price resolver.
* Existing ProductVariant fallback behavior.
* Existing cart/add/sync/checkout behavior.
* Existing product detail behavior.
* Existing Admin/WebApp compatibility.
* Existing migrations.
* Existing Orders/Wallet behavior.

### 9. Logging / Reporting

If existing logging is available, log write-mode backfill summary.

Do not log every row unless current style supports it and volume is small.

Final report must clearly state:

```text id="42arqf"
- whether this is dry-run capable;
- whether it writes only missing rows;
- whether fallback remains active;
- whether actual database backfill was executed.
```

### 10. Strict Out of Scope

Do not:

* Modify Admin Web.
* Modify Customer WebApp.
* Remove ProductVariant fallback.
* Enforce missing ShopProductVariant rejection.
* Change runtime cart/checkout pricing rules.
* Add shop-level stock/capacity override.
* Add reservation/ledger.
* Add remaining capacity display.
* Modify Orders/Wallet.
* Create data rows in a real database.
* Apply migrations to a real database.
* Change auth/security.
* Change Docker/GitHub Actions.
* Change package versions or target frameworks.
* Mark production-ready.

## Validation

From backend repository root, run:

```bash id="zkctw5"
dotnet restore .\Refahi.Backend.slnx
dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore
dotnet test .\Refahi.Backend.slnx --configuration Release --no-build
```

If Store tests exist or can be added safely, add focused tests for:

```text id="6545ww"
- Audit counts missing offerings correctly.
- Dry-run backfill creates no rows.
- Write backfill creates missing rows.
- Second write backfill is idempotent.
- Existing offering price is not overwritten.
- Invalid variant price is skipped/warned.
```

Do not introduce a new test framework if no Store test infrastructure exists.

Do not claim validation passed unless commands actually pass.

## Manual QA

Manual API QA is optional.

If local API/auth/test data exists, test:

```text id="h4hksd"
- Run audit for all shops/products.
- Run dry-run backfill for a product with variants.
- Run write backfill for that product.
- Re-run audit and confirm missing count decreases.
- Re-run write backfill and confirm created count is zero.
- Verify existing configured ShopProductVariant prices are not overwritten.
```

If manual QA is not possible, report it as not run.

## Final Report Format

Save the report to:

```text id="q12pgb"
.codex/reports/phase-25a-shopproductvariant-backfill-audit-tooling.md
```

Use exactly this structure:

```text id="gb8tg3"
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

Audit contract/API:
- ...

Backfill contract/API:
- ...

Backfill behavior:
- ...

Safety/idempotency:
- ...

Repository/persistence changes:
- ...

Backward compatibility:
- ...

Validation:
- Command: ...
  Result: ...

Manual QA:
- ...

Fallback behavior:
- ...

Deferred items:
- ...

Risks / assumptions:
- ...

Suggested next phase:
- ...
```
