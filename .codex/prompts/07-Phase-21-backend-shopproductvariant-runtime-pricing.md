# Codex Implementation Prompt — Phase 21: Backend Runtime Price Resolution for ShopProductVariant

Read `.codex/prompts/_session-bootstrap.md` first.
Then read `AGENTS.md`.
Load only the relevant backend/module/store/order architecture skills from `.codex/skills/`.

This is a focused Backend API implementation task.

Run this prompt from:

```text id="lb68i9"
C:\Workspace\repo\refahiplus-api
```

Do not modify Admin Web.
Do not modify Customer WebApp.
Do not modify Docker/GitHub Actions.
Do not remove fallback behavior in this phase.
Do not remove `ShopProduct`.
Do not remove `ProductVariant.PriceMinor`.
Do not remove `ProductVariant.StockCount`.
Do not add shop-level stock/capacity override.
Do not implement reservation/ledger in this phase.

## Mandatory Report Output

At the end of this task, save the final report as:

```text id="g7trcj"
.codex/reports/phase-21-backend-shopproductvariant-runtime-pricing.md
```

If `.codex/reports/` does not exist, create it.

Also print the same report in the Codex final response.

## Context

Phase 18 selected Option B:

```text id="cu0tzc"
ShopProduct remains the parent shop/product listing.
ShopProductVariant is the shop-specific variant offering row.
```

Phase 19 implemented backend support for `ShopProductVariant`:

```text id="pz8kr7"
ShopProductVariant:
- Id
- ShopProductId
- ProductVariantId
- PriceMinor
- DiscountedPriceMinor
- IsActive
- IsDeleted
- CreatedAt
- UpdatedAt
```

Phase 20 added Admin UI for managing shop-specific product variant offerings.

Current state:

* Admin can configure `ShopProductVariant` prices.
* Existing cart/checkout/product detail still use old pricing behavior.
* For variant purchases, backend currently still uses `ProductVariant.PriceMinor` / `DiscountedPriceMinor`.
* For non-variant purchases, backend uses `ShopProduct.Price` / `DiscountedPrice`.
* Existing data does not yet have guaranteed `ShopProductVariant` rows for all product variants.

Target runtime pricing strategy from Phase 18:

```text id="qydxg2"
If no VariantId:
  authoritative price = ShopProduct price/discount.

If VariantId:
  authoritative price = active ShopProductVariant price/discount.

Temporary migration fallback:
  if no active ShopProductVariant row exists yet, fallback to ProductVariant price/discount.

After backfill/manual QA in a later phase:
  remove fallback and reject missing ShopProductVariant.
```

Important distinction:

```text id="0tn28o"
Missing offering row:
- allowed temporarily with fallback to ProductVariant price.

Existing offering row but inactive:
- should be treated as unavailable/rejected for variant purchase.

Deleted offering row:
- treat like missing row for this phase only if no non-deleted row exists, allowing fallback.
```

## Goal

Introduce a centralized backend Store price resolver and use it consistently in runtime purchase paths.

After this phase:

* Variant runtime price can come from `ShopProductVariant`.
* Non-variant runtime price still comes from `ShopProduct`.
* Temporary fallback to `ProductVariant` price exists for variant products without configured shop offering rows.
* AddToCart uses the resolver.
* SyncCart uses the resolver and can detect price changes.
* GetCart projects current resolved price where appropriate.
* UpdateCartItem preserves validated price/quantity behavior.
* PlaceStoreOrder re-resolves authoritative price before creating Orders.
* Order metadata includes `shop_product_variant_id` when available.
* Existing StockBased, SessionBased legacy, and variant-capacity flows remain working.
* Admin/WebApp source code remains untouched.

## Repository

Primary repository:

```text id="nbhizv"
C:\Workspace\repo\refahiplus-api
```

Do not modify:

```text id="y75nmn"
C:\Workspace\repo\refahiplus-admin
C:\Workspace\repo\refahiplus-webapp
```

## Required Pre-Check

Run:

```bash id="5ou9xk"
git status --short
```

Report dirty state clearly.

If the repo is not clean, preserve existing user changes and do not touch unrelated files.

## Required Investigation Before Editing

Inspect actual code and follow existing conventions.

Search for:

```text id="ytxtfv"
ShopProduct
ShopProductVariant
ProductVariant
PriceMinor
DiscountedPriceMinor
UnitPriceMinor
FinalPriceMinor
AddToCartCommandHandler
SyncCartCommandHandler
GetCartQueryHandler
UpdateCartItemCommandHandler
PlaceStoreOrderCommandHandler
StoreOrderPaidEventHandler
CartItem
CartItemDto
MetadataJson
ProductDetail
GetProductBySlug
AdminGetProduct
ShopProductsEndpoints
IShopProductRepository
ShopProductRepository
```

Likely areas:

```text id="htumoh"
src/Refahi.Modules.Store.Application/
src/Refahi.Modules.Store.Application.Contracts/
src/Refahi.Modules.Store.Domain/
src/Refahi.Modules.Store.Infrastructure/
src/Refahi.Modules.Store.Api/
src/Refahi.Modules.Orders.Application.Contracts/
src/Refahi.Modules.Orders.Application/
```

Do not assume exact file names. Use actual structure.

## Required Implementation Areas

### 1. Central Store Price Resolver

Add a centralized Store Application service.

Suggested name:

```text id="3m6ri0"
StoreProductPriceResolver
```

or follow existing service naming conventions.

The resolver should accept enough context:

```csharp id="zp9wfg"
ShopId
ProductId
Guid? VariantId
Guid? SessionId
```

It may also accept loaded product/shop product objects if existing handlers already load them.

The resolver should return a result object similar to:

```csharp id="9bx5jw"
public sealed record StoreResolvedPrice(
    long UnitPriceMinor,
    long? OriginalPriceMinor,
    long? DiscountedPriceMinor,
    Guid ShopProductId,
    Guid? ShopProductVariantId,
    Guid? VariantId,
    StorePriceSource Source,
    bool UsedFallback
);
```

Adapt shape to current project style.

Suggested enum:

```csharp id="63iln2"
public enum StorePriceSource
{
    ShopProduct = 0,
    ShopProductVariant = 1,
    ProductVariantFallback = 2
}
```

Rules:

#### No VariantId

```text id="4dp1t0"
- Require active/non-deleted ShopProduct.
- Price comes from ShopProduct.
- ShopProductVariant is not involved.
```

#### VariantId present

```text id="x4zme6"
- Require active/non-deleted ShopProduct.
- Require ProductVariant belongs to Product.
- If active non-deleted ShopProductVariant exists:
    use ShopProductVariant price.
    include ShopProductVariantId.
    Source = ShopProductVariant.
- If non-deleted ShopProductVariant exists but IsActive == false:
    reject as unavailable for this shop.
- If no non-deleted ShopProductVariant exists:
    temporarily fallback to ProductVariant price.
    Source = ProductVariantFallback.
    UsedFallback = true.
```

Do not use `Capacity` as price.
Do not use `StockCount` as price.
Do not change capacity enforcement logic except to keep it compatible.

### 2. Price Validation Rules

Use existing money/price validation conventions.

Resolver should guard:

```text id="ql4j0b"
- Price must be > 0.
- Discounted price, if present, must be > 0 and < price.
```

For final unit price:

```text id="2clqft"
UnitPriceMinor = DiscountedPriceMinor ?? PriceMinor
```

If current code uses a different convention, preserve current behavior while making it explicit.

### 3. Repository Support

Add only necessary repository methods.

Expected needs:

```csharp id="x7oela"
Task<ShopProduct?> GetByShopAndProductWithVariantOfferingsAsync(Guid shopId, Guid productId, CancellationToken cancellationToken);
Task<ShopProductVariant?> GetVariantOfferingAsync(Guid shopId, Guid productId, Guid variantId, CancellationToken cancellationToken);
```

or equivalent.

Avoid direct EF queries scattered across handlers.
Prefer central resolver using repository/query abstractions.

### 4. AddToCart Runtime Pricing

Update AddToCart flow.

Current behavior must be preserved for:

```text id="i3z5qu"
- StockBased no variant
- StockBased with variant
- legacy SessionBased with SessionId
- SessionBased variant-capacity with UsageDate
```

New behavior:

```text id="876n58"
- Resolve price through central resolver.
- If VariantId exists and active ShopProductVariant exists, cart UnitPriceMinor uses ShopProductVariant price.
- If VariantId exists and no offering exists, temporarily fallback to ProductVariant price.
- If inactive offering exists, reject add-to-cart with Persian error.
```

Suggested Persian error:

```text id="a1twhc"
این تنوع در فروشگاه انتخاب‌شده فعال نیست.
```

### 5. SyncCart Runtime Pricing

Update SyncCart flow.

Expected behavior:

* Re-resolve price through central resolver for every synced item.
* If local snapshot price differs from resolved price, follow existing sync behavior for price changes.
* If existing sync warning type exists, use it.
* If no specific warning exists, add or use a compatible warning such as:

```text id="xrvepr"
PRICE_CHANGED
```

Rules:

```text id="gttjy3"
- StockBased/non-variant remains compatible.
- Legacy SessionId remains compatible.
- Variant-capacity with UsageDate remains compatible.
- Inactive ShopProductVariant should cause item drop or warning, following existing unavailable item sync behavior.
- Missing offering row should fallback to ProductVariant price in this phase.
```

### 6. GetCart Projection

Update GetCart projection so cart DTOs reflect resolved/current runtime price when appropriate.

Options:

```text id="s7dn25"
A. Keep CartItem.UnitPriceMinor snapshot and expose current/resolved price separately.
B. Update projection to show resolved price and indicate price changed.
```

Choose the least disruptive approach based on current DTOs.

Do not silently hide price changes if the project already exposes warnings/status.

If adding fields is safe, consider:

```csharp id="fg7ffw"
long? CurrentUnitPriceMinor
bool HasPriceChanged
Guid? ShopProductVariantId
string? PriceSource
```

Only add fields if consistent with existing DTO evolution.

At minimum, ensure GetCart does not incorrectly display global ProductVariant price when an active ShopProductVariant price exists.

### 7. UpdateCartItem Quantity Flow

If quantity update revalidates stock/capacity but does not re-resolve price, inspect whether this is acceptable.

Recommended:

```text id="bx0kiq"
- Re-resolve price during quantity update if the cart item has VariantId/ProductId/ShopId.
- Preserve existing UnitPriceMinor update strategy.
- Do not change quantity semantics beyond existing validation.
```

Do not break capacity enforcement from Phase 16.

### 8. PlaceStoreOrder Authoritative Repricing

Update PlaceStoreOrder so it does not blindly trust stale `CartItem.UnitPriceMinor`.

Expected behavior:

* Re-resolve price for each cart item before creating Order.
* If resolved price differs from cart snapshot:

  * Prefer rejecting order with clear Persian error asking user to refresh cart.
  * Or update cart item before order only if existing checkout flow already supports automatic price refresh.
* Do not create Orders with stale prices silently.

Suggested Persian error:

```text id="0tw9ky"
قیمت برخی آیتم‌های سبد خرید تغییر کرده است. لطفاً سبد خرید را به‌روزرسانی و دوباره تلاش کنید.
```

For each OrderItem metadata:

* Keep existing metadata.
* Keep `variant_id`.
* Keep `session_id` where applicable.
* Keep `usage_date` where applicable.
* Add `shop_product_variant_id` when resolver returns one.
* Add optional price source metadata if useful and safe:

```json id="bp2esw"
{
  "shop_product_variant_id": "...",
  "price_source": "ShopProductVariant"
}
```

Do not modify Orders/Wallet contracts unless unavoidable.

### 9. Product Detail Backend Projection

Inspect backend product detail/admin product detail projection.

Goal for backend readiness:

* Public/customer product detail DTO should be capable of returning shop-specific variant prices.
* If product detail endpoint currently resolves a `ShopProduct`, variant DTOs should include shop-specific price where active offering exists.
* Missing offering row should fallback to ProductVariant price in this phase.
* Inactive offering should mark variant unavailable for that shop or exclude it, depending on current DTO design.

Important:

Do not modify Customer WebApp in this phase.

Backend DTO additions are acceptable if backward compatible.

Potential fields on variant DTO:

```csharp id="bqryy0"
Guid? ShopProductVariantId
long? ShopPriceMinor
long? ShopDiscountedPriceMinor
string? PriceSource
bool IsActiveInShop
bool UsesShopSpecificPrice
```

Only add fields if current DTO style supports it.

At minimum, keep existing fields populated with effective runtime price so WebApp can be updated in Phase 22.

### 10. Admin Product Detail Compatibility

Do not break Admin product detail.

Admin still needs base `ProductVariant.PriceMinor` for “copy base price” in Phase 20 UI.

If you change shared `ProductVariantDto` fields, ensure Admin can still access base variant price.

Prefer adding fields instead of replacing existing base price fields.

### 11. StoreOrderPaidEventHandler

Do not change stock/capacity finalization unless needed for metadata compatibility.

Expected behavior remains:

* StockBased product stock decrement unchanged.
* StockBased variant stock decrement unchanged.
* Legacy ProductSession sell unchanged.
* SessionBased variant-capacity paid-event defensive capacity recheck unchanged.
* No price changes after order creation.

### 12. Temporary Fallback Tracking

Because backfill has not happened yet, fallback is required.

When fallback to `ProductVariant` price is used:

* Keep behavior working.
* Consider logging if existing logging style exists.
* Optionally include metadata `price_source = ProductVariantFallback`.
* Report all fallback behavior clearly.

Do not remove fallback in this phase.

### 13. Backward Compatibility

Preserve:

* Existing ShopProduct APIs.
* Existing ShopProductVariant admin APIs.
* Existing Admin Web compatibility.
* Existing Customer WebApp compatibility.
* Existing stock-based cart.
* Existing legacy ProductSession cart.
* Existing variant-capacity UsageDate cart.
* Existing order/wallet behavior.
* Existing public route paths.
* Existing API response shapes unless adding backward-compatible optional fields.

### 14. Strict Out of Scope

Do not:

* Modify Admin Web.
* Modify Customer WebApp.
* Add shop product variant backfill.
* Remove fallback to ProductVariant price.
* Remove `ShopProduct.Price`.
* Remove `ProductVariant.PriceMinor`.
* Remove `ProductVariant.StockCount`.
* Remove `ProductSession`.
* Add shop-level capacity override.
* Add shop-level stock override.
* Add reservation/ledger.
* Change Wallet payment rules.
* Add remaining-capacity display.
* Change auth/security.
* Change Docker/GitHub Actions.
* Change package versions or target frameworks.
* Mark production-ready.

## Validation

From backend repository root, run:

```bash id="czzb1i"
dotnet restore .\Refahi.Backend.slnx
dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore
dotnet test .\Refahi.Backend.slnx --configuration Release --no-build
```

If Store tests exist or can be added safely, add focused tests for:

```text id="h11trr"
- Non-variant item resolves ShopProduct price.
- Variant item resolves active ShopProductVariant price.
- Variant item falls back to ProductVariant price when offering is missing.
- Variant item is rejected when offering exists but inactive.
- PlaceStoreOrder rejects stale cart price.
- Metadata includes shop_product_variant_id when available.
```

Do not introduce a new test framework if no Store test infrastructure exists.

Do not claim validation passed unless commands actually pass.

## Manual QA

Manual API QA is optional.

If local API/auth/test data is available, test:

```text id="k3p7vx"
- Add non-variant product to cart: price from ShopProduct.
- Add variant product with active ShopProductVariant: price from ShopProductVariant.
- Add variant product without ShopProductVariant: fallback to ProductVariant.
- Add variant product with inactive ShopProductVariant: reject.
- Checkout after changing shop variant price: reject stale cart price or force refresh.
- Order metadata includes shop_product_variant_id for shop variant price.
```

If manual QA is not possible, report it as not run.

## Final Report Format

Save the report to:

```text id="pd7zmo"
.codex/reports/phase-21-backend-shopproductvariant-runtime-pricing.md
```

Use exactly this structure:

```text id="tez34u"
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

Price resolver:
- ...

Repository changes:
- ...

AddToCart pricing:
- ...

SyncCart pricing:
- ...

GetCart pricing/projection:
- ...

UpdateCartItem behavior:
- ...

PlaceStoreOrder repricing:
- ...

Order metadata:
- ...

Product detail projection:
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
