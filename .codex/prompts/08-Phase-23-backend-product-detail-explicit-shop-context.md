# Codex Implementation Prompt — Phase 23: Backend Explicit Shop Context for Product Detail Pricing

Read `.codex/prompts/_session-bootstrap.md` first.
Then read `AGENTS.md`.
Load only the relevant backend/module/store architecture skills from `.codex/skills/`.

This is a focused Backend API implementation task.

Run this prompt from:

```text id="t5nmms"
C:\Workspace\repo\refahiplus-api
```

Do not modify Admin Web.
Do not modify Customer WebApp.
Do not modify Docker/GitHub Actions.
Do not remove existing product detail route compatibility.
Do not remove runtime price fallback behavior.
Do not remove `ShopProduct`.
Do not remove `ProductVariant.PriceMinor`.
Do not remove `ShopProductVariant`.
Do not change cart/checkout behavior except if unavoidable for shared DTO compatibility.
Do not implement backfill in this phase.

## Mandatory Report Output

At the end of this task, save the final report as:

```text id="zr2ck1"
.codex/reports/phase-23-backend-product-detail-explicit-shop-context.md
```

If `.codex/reports/` does not exist, create it.

Also print the same report in the Codex final response.

## Context

The Store pricing model now supports shop-specific product variant pricing.

Completed phases:

* Phase 19 introduced backend `ShopProductVariant` management.
* Phase 20 added Admin UI for managing shop-specific variant offerings.
* Phase 21 introduced backend runtime price resolution:

  * non-variant price resolves from `ShopProduct`;
  * variant price resolves from active `ShopProductVariant`;
  * missing shop variant offering temporarily falls back to `ProductVariant`;
  * inactive shop variant offering is rejected;
  * cart/add/sync/get/update and checkout use the backend resolver.
* Phase 22 updated Customer WebApp to consume shop-specific variant pricing fields from product detail and cart DTOs.

Current blocker found in Phase 22:

```text id="vvgi69"
WebApp product detail calls:
/api/store/{moduleSlug}/products/{slug}

It does not pass ShopSlug or ShopId to the product API.

The WebApp page may later overlay ShopId/ShopName/ShopSlug from a separate shop lookup, but the product API response has already been loaded.

Backend GetProductBySlugQuery currently accepts only product slug and resolves variant offering prices using the existing first-active-ShopProduct behavior.

Therefore exact shop-specific variant pricing is not guaranteed when the same product exists in multiple shops.
```

## Goal

Make backend public product detail retrieval support an explicit shop context while preserving backward compatibility.

After this phase:

* Public product detail API can accept explicit shop context:

  * preferably `shopSlug`;
  * optionally `shopId` if current route/API conventions support it safely.
* `GetProductBySlugQuery` or equivalent application query carries optional shop context.
* Backend product detail projection resolves `ShopProduct` using the explicit shop context when provided.
* Variant price fields are resolved from `ShopProductVariant` for that exact shop.
* Inactive shop variant offerings are marked inactive/unavailable for that shop.
* Missing shop variant offerings still use temporary `ProductVariant` fallback.
* Existing no-shop-context product detail behavior remains backward compatible, but should be clearly marked as legacy/fallback in code/report.
* Admin product detail remains unaffected.
* Customer WebApp source remains untouched in this phase.

## Repository

Primary repository:

```text id="ak66h6"
C:\Workspace\repo\refahiplus-api
```

Do not modify:

```text id="hak3gh"
C:\Workspace\repo\refahiplus-admin
C:\Workspace\repo\refahiplus-webapp
```

## Required Pre-Check

Run:

```bash id="v2v556"
git status --short
```

Report dirty state clearly.

If the repo is not clean, preserve existing user changes and do not touch unrelated files.

## Required Investigation Before Editing

Inspect actual code and follow existing conventions.

Search for:

```text id="h5c33s"
GetProductBySlug
GetProductBySlugQuery
ProductBySlug
ProductDetail
ProductVariantDto
ShopSlug
ShopId
ShopProduct
ShopProductVariant
StoreProductPriceResolver
ShopProductsEndpoints
ProductsEndpoints
moduleSlug
products/{slug}
GetShop
Shop
Slug
IsActive
IsDeleted
```

Likely areas:

```text id="q5672y"
src/Refahi.Modules.Store.Api/
src/Refahi.Modules.Store.Api/Endpoints/
src/Refahi.Modules.Store.Application.Contracts/
src/Refahi.Modules.Store.Application/Features/Products/GetProductBySlug/
src/Refahi.Modules.Store.Application/Services/StoreProductPriceResolver.cs
src/Refahi.Modules.Store.Domain/
src/Refahi.Modules.Store.Infrastructure/Repositories/
src/Refahi.Modules.Store.Infrastructure/Persistence/
```

Do not assume names. Use actual project structure.

## Required Implementation Areas

### 1. Public Product Detail API Contract

Extend the public product detail endpoint to accept optional explicit shop context.

Preferred contract:

```http id="a3cbka"
GET /api/store/{moduleSlug}/products/{slug}?shopSlug={shopSlug}
```

If current API conventions make `shopId` easier or already used, support:

```http id="btdk46"
GET /api/store/{moduleSlug}/products/{slug}?shopId={shopId}
```

Preferred final behavior:

```text id="7y4vfv"
- Accept shopSlug as optional query parameter.
- Optionally accept shopId as optional query parameter.
- If both are provided, validate that they point to the same shop or reject with clear error.
```

Do not break the old call without shop context.

### 2. Application Query Update

Update `GetProductBySlugQuery` or equivalent to include optional shop context.

Suggested shape:

```csharp id="jnij5a"
public sealed record GetProductBySlugQuery(
    string Slug,
    string? ShopSlug = null,
    Guid? ShopId = null
);
```

Adapt to current query style.

Rules:

* Existing callers that pass only slug must still compile/work.
* Admin product detail query should not be affected unless it shares the same query intentionally.
* Public customer product detail should use explicit shop context when provided.

### 3. Shop Context Resolution

Add or update application logic to resolve the correct `ShopProduct`.

Expected behavior:

#### If `shopId` is provided

```text id="s4ra2f"
- Find active/non-deleted ShopProduct for ProductId + ShopId.
- If missing, return not found or unavailable according to current product detail conventions.
```

#### If `shopSlug` is provided

```text id="reushs"
- Resolve Shop by slug.
- Find active/non-deleted ShopProduct for ProductId + resolved ShopId.
- If missing, return not found or unavailable according to current product detail conventions.
```

#### If both `shopId` and `shopSlug` are provided

```text id="w0jrzg"
- Ensure both refer to the same shop.
- If mismatch, return validation error.
```

#### If no shop context is provided

```text id="ylx480"
- Preserve current first-active-ShopProduct behavior for backward compatibility.
- Clearly isolate/label this path as legacy fallback.
- Do not remove it in this phase.
```

### 4. Repository Support

Use existing repositories if available.

If necessary, add narrow repository methods.

Possible methods:

```csharp id="n3uhbk"
Task<Shop?> GetBySlugAsync(string slug, CancellationToken cancellationToken);
Task<ShopProduct?> GetActiveByShopAndProductWithVariantOfferingsAsync(Guid shopId, Guid productId, CancellationToken cancellationToken);
```

or equivalent.

Avoid scattered EF queries in handlers.
Prefer repository/application service patterns already used in Store.

### 5. Product Detail Variant Price Resolution

Update public product detail projection so variant pricing uses the resolved `ShopProduct` context.

For each variant:

```text id="9afhg5"
If active ShopProductVariant exists for resolved ShopProduct:
  expose shop-specific price fields.
  mark IsActiveInShop = true.
  UsesShopSpecificPrice = true.
  PriceSource = ShopProductVariant.

If non-deleted ShopProductVariant exists but IsActive == false:
  mark variant inactive/unavailable in shop.
  do not expose it as selectable.

If no non-deleted ShopProductVariant exists:
  use ProductVariant fallback price.
  mark PriceSource = ProductVariantFallback.
  UsesShopSpecificPrice = false.
```

Preserve fields added in Phase 21:

```text id="v8v5aw"
ShopProductVariantId
ShopPriceMinor
ShopDiscountedPriceMinor
PriceSource
IsActiveInShop
UsesShopSpecificPrice
```

If exact field names differ, follow current backend DTO.

Important:

* Do not replace Admin base variant price fields.
* Public product detail may expose effective/shop fields, but `PriceMinor` / `DiscountedPriceMinor` compatibility should be preserved according to current DTO behavior.
* Do not compute remaining capacity.
* Do not change UsageDate/capacity logic.

### 6. Product-Level Price Resolution

For product detail parent price:

```text id="b85m3y"
- If explicit shop context is provided, product-level price should come from that shop's ShopProduct.
- If no shop context, preserve existing first-active-ShopProduct fallback.
```

Do not show price from a different shop when explicit shop context is provided.

### 7. Error Semantics

Follow existing `Result`, `Error`, or exception conventions.

Suggested Persian messages if needed:

```text id="zc0mnr"
این محصول در فروشگاه انتخاب‌شده فعال نیست.
فروشگاه انتخاب‌شده معتبر نیست.
اطلاعات فروشگاه با درخواست محصول هم‌خوانی ندارد.
```

Use existing error code style.

Decision guidance:

* If product exists globally but is not active in requested shop, prefer `NotFound` or domain unavailable result consistent with current public product detail behavior.
* Do not leak inactive/deleted product/shop details if current API avoids it.

### 8. Backward Compatibility

Preserve:

* Existing product detail route.
* Existing product detail call without shop context.
* Existing Admin product detail behavior.
* Existing cart/add/sync/checkout behavior.
* Existing price resolver fallback.
* Existing `ProductVariant` base price fields.
* Existing `ShopProductVariant` admin endpoints.
* Existing Store module route conventions.
* Existing WebApp compatibility.

### 9. Strict Out of Scope

Do not:

* Modify Customer WebApp.
* Modify Admin Web.
* Add database migrations unless absolutely required.
* Add backfill.
* Remove ProductVariant fallback.
* Remove first-active-ShopProduct fallback for no-shop-context requests.
* Remove ProductVariant base price fields.
* Remove ShopProduct price fields.
* Change cart/checkout pricing behavior.
* Add reservation/ledger.
* Add stock/capacity override.
* Implement remaining capacity display.
* Change Orders/Wallet.
* Change auth/security.
* Change Docker/GitHub Actions.
* Change package versions or target frameworks.
* Mark production-ready.

## Validation

From backend repository root, run:

```bash id="gsfa9p"
dotnet restore .\Refahi.Backend.slnx
dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore
dotnet test .\Refahi.Backend.slnx --configuration Release --no-build
```

If Store tests exist or can be added safely, add focused tests for:

```text id="wz5k01"
- Product detail without shop context still works.
- Product detail with shopSlug resolves that exact shop product.
- Product detail with shopId resolves that exact shop product.
- shopSlug/shopId mismatch is rejected.
- Product not available in requested shop returns expected unavailable/not-found result.
- Variant price comes from ShopProductVariant for requested shop.
- Missing ShopProductVariant falls back to ProductVariant price.
- Inactive ShopProductVariant marks variant inactive/unavailable.
```

Do not introduce a new test framework if no Store test infrastructure exists.

Do not claim validation passed unless commands actually pass.

## Manual QA

Manual API QA is optional.

If local API/auth/test data exists, test:

```text id="z4h5oq"
- Same product in two shops with different ShopProductVariant prices.
- Call product detail with shopSlug A and verify variant price A.
- Call product detail with shopSlug B and verify variant price B.
- Call product detail with no shop context and verify legacy fallback still works.
- Inactive ShopProductVariant is disabled/unavailable.
- Missing ShopProductVariant uses fallback price.
```

If manual QA is not possible, report it as not run.

## Final Report Format

Save the report to:

```text id="wk2eaj"
.codex/reports/phase-23-backend-product-detail-explicit-shop-context.md
```

Use exactly this structure:

```text id="ef1y49"
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

API contract changes:
- ...

Query/application changes:
- ...

Shop context resolution:
- ...

Product-level price behavior:
- ...

Variant price behavior:
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
