# Codex Planning Prompt — Phase 18: ShopProductVariant and Shop-Specific Variant Pricing Plan

Read `.codex/prompts/_session-bootstrap.md` first.
Then read `AGENTS.md`.
Load only relevant backend, order, store, admin, and rendering skills from `.codex/skills/`.

This is a **planning-only task**.

Do not modify source files.
Do not create migrations.
Do not implement code.
Only inspect the codebase and create the required planning report.


Admin and WebApp repositories may be inspected if accessible, but must not be modified.

## Mandatory Report Output

At the end of this task, save the final report as:

```text id="4kizce"
.codex/reports/05-phase-18-shopproductvariant-pricing-impact-plan.md
```

Also print the same report in the Codex final response.

## Context

Before the variant-capacity refactor, the Store/SupplyChain model was roughly:

```text id="7luy5k"
Supplier
 └── Agreements
      └── AgreementProducts
           └── Category / SalesModel / ProductType / DeliveryType

Supplier also has:
 └── Products
      └── Category must be allowed by AgreementProducts

Supplier also has:
 └── Shops
      └── ShopProducts
           └── each shop can sell selected products
           └── each product can have shop-specific price/discount
```

Current `ShopProduct` is a mapping between `Shop` and `Product`.

Known current characteristics from previous analysis:

* `ShopProduct` is not an inventory model.
* `ShopProduct` does not copy/override `ProductVariant.StockCount`.
* `ShopProduct` does not copy/override `ProductVariant.Capacity`.
* `ShopProduct` has shop-level price/discount/description/active state.
* `ProductVariant` is now becoming the true sellable option for variant-based and session/access products.

After recent refactors:

* `ProductVariant` has:

  * `FromDate`
  * `ToDate`
  * `CapacityType`
  * `Capacity`
  * `RequiresUsageDate`
* `ProductVariant.StockCount` remains legacy physical inventory for StockBased variants.
* `ProductVariant.Capacity` is service/access capacity for SessionBased/access variants.
* WebApp and backend now carry `UsageDate`.
* Backend enforces variant capacity for SessionBased variant-capacity products.

New logical challenge:

```text id="9wwstl"
When adding a product to a shop, should the shop add Product or ProductVariant?

How should shop-specific pricing work now that the true sellable option may be ProductVariant?
```

Proposed direction to analyze:

```text id="al03q2"
Keep ShopProduct as the parent shop/product listing.
Add ShopProductVariant as the shop-specific sellable variant offering.

Product without variants:
- ShopProduct is the sellable listing and carries shop price.

Product with variants:
- ShopProduct is the parent listing.
- ShopProductVariant defines which variants are available in that shop and their shop-specific prices.
```

Possible new entity:

```csharp id="csvvt8"
ShopProductVariant
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

Initial recommendation:

* Do not put capacity in ShopProductVariant in v1.
* Keep capacity on ProductVariant.
* Use ShopProductVariant for shop-specific price/discount/activation only.
* Defer shop-level variant stock/capacity override unless the business explicitly needs independent per-shop allocations.

## Goal

Create a detailed impact analysis and implementation plan for introducing `ShopProductVariant` and shop-specific variant pricing.

The plan must answer:

```text id="79gpxt"
1. Should shops add Product or ProductVariant?
2. How should shop-specific prices be stored for products with variants?
3. How should existing ShopProduct price behavior remain backward compatible?
4. How should cart/checkout resolve the actual sale price?
5. What changes are needed in Backend, Admin, and WebApp?
```

## Repositories To Inspect

Primary Backend repository:

```text id="v1pkuh"
C:\Workspace\repo\refahiplus-api
```

Admin repository, if accessible:

```text id="8b7r2d"
C:\Workspace\repo\refahiplus-admin
```

Customer WebApp repository, if accessible:

```text id="74r9zg"
C:\Workspace\repo\refahiplus-webapp
```

Do not modify any repository.

## Required Pre-Check

Run:

```bash id="87g2l5"
git status --short
```

in every inspected repository.

Report dirty state clearly.

If any repo has uncommitted user changes, do not modify anything and avoid assuming the final intended state unless the code clearly shows it.

## Required Investigation Areas

Search and inspect all references to:

```text id="n9lvp7"
ShopProduct
ShopProductsEndpoints
AddShopProduct
UpdateShopProduct
IShopProductRepository
ShopProductRepository
ShopProductConfiguration
ProductVariant
ProductVariantDto
ProductVariant.StockCount
Capacity
CapacityType
Price
PriceMinor
DiscountedPrice
DiscountedPriceMinor
AddToCart
SyncCart
PlaceStoreOrder
OrderItem.MetadataJson
ProductDetail
ProductPurchaseIsland
ProductVariantSelector
AdminApiClient
ShopProductsPage
AddEditShopProductDialog
```

Group findings by:

* Store Domain
* Store Application
* Store API
* Store Infrastructure / EF / migrations
* SupplyChain / AgreementProduct
* Admin Web
* Customer WebApp
* Cart
* Checkout / Orders
* Historical compatibility / existing data

## Planning Questions

### 1. Current ShopProduct Model

Analyze current `ShopProduct`.

Answer:

* What fields does it have?
* Is it aggregate/entity/value object?
* Does it own price?
* Does it own discount?
* Does it own description?
* Does it own active/deleted state?
* Does it reference variants?
* Does it impact inventory/capacity?
* How is it mapped in EF?
* What unique constraints/indexes exist?
* What APIs/commands update it?
* How is it used by Admin?
* How is it used by Customer product detail/cart?

### 2. Current ProductVariant Pricing Model

Analyze current `ProductVariant`.

Answer:

* Where is `PriceMinor` stored?
* Where is `DiscountedPriceMinor` stored?
* How are variant prices projected to DTOs?
* Does cart/checkout use variant price or ShopProduct price?
* What happens when ShopProduct has a price but variant also has a price?
* Is there a current fallback order?
* Is price snapshot written into `OrderItem`?
* Are prices stored in metadata or first-class order item fields?

### 3. Product vs Variant as Shop Offering

Evaluate these options:

#### Option A — Shop adds Product only

* ShopProduct remains the only shop listing.
* Variant prices remain global on ProductVariant.
* Shop-specific price applies only to non-variant products.

#### Option B — Shop adds Product and selects variants

* ShopProduct remains parent listing.
* New ShopProductVariant defines selected variants and shop-specific variant prices.

#### Option C — Shop adds variants directly

* ShopProductVariant or equivalent becomes primary listing.
* Product-level ShopProduct is reduced or removed.

Recommend the safest option.

Consider:

* Existing data.
* Admin UX.
* Customer product detail.
* Cart/checkout price resolution.
* Future shop-specific availability.
* Migration risk.

### 4. Proposed ShopProductVariant Model

If recommended, design `ShopProductVariant`.

Answer:

* Should it be under Store Domain?
* Should it be child of ShopProduct?
* What fields should it have in v1?
* Should it have price/discount?
* Should it have description?
* Should it have IsActive/IsDeleted?
* Should it have display order?
* Should it have stock/capacity override?
* Should it have validity override?
* Should uniqueness be:

  * one active row per ShopProductId + ProductVariantId?
  * or per ShopId + ProductId + ProductVariantId?
* What indexes are needed?

Recommended v1 default:

```text id="e0jmiq"
ShopProductVariant:
- ShopProductId
- ProductVariantId
- PriceMinor
- DiscountedPriceMinor
- IsActive
- IsDeleted
- CreatedAt
- UpdatedAt
```

Do not include capacity override unless a strong reason exists.

### 5. Pricing Resolution Strategy

Define authoritative price resolution.

Suggested target:

```text id="y4jawt"
If Product has no selected Variant:
  price = ShopProduct.Price / DiscountedPrice

If Product has Variant:
  price = ShopProductVariant.Price / DiscountedPrice

Fallback only during migration:
  if ShopProductVariant missing, fallback to ProductVariant.Price / DiscountedPrice
  but report as transitional behavior
```

Answer:

* Should fallback be allowed?
* Should backend reject variant add-to-cart if ShopProductVariant is missing/inactive?
* Should ShopProduct price continue to matter for variant products?
* Should ProductVariant price become base/reference price only?
* What price should be displayed in product detail?
* What price should be used in cart/order?
* What price should be snapshotted into order?

### 6. Cart / Checkout / Order Impact

Analyze and plan:

* AddToCart currently validates shop/product relation through ShopProduct.
* How should it validate variant relation through ShopProductVariant?
* If `VariantId` is supplied, should backend require active ShopProductVariant?
* How should cart item price be resolved?
* How should `PlaceStoreOrder` resolve price?
* Should `OrderItem.MetadataJson` include `shop_product_variant_id`?
* Should `variant_id` remain in metadata?
* How do capacity and UsageDate interact with ShopProductVariant?
* What should happen if variant is active on product but not enabled for the shop?
* What should happen if ShopProduct is inactive but ShopProductVariant is active?
* What happens when a ShopProductVariant price changes while item is in cart?

### 7. Admin UI/UX Plan

Analyze current Admin shop-product UI.

Plan the new UX:

* Add product to shop.
* If product has no variants:

  * keep existing price/discount fields.
* If product has variants:

  * show variant list.
  * allow selecting all/some variants.
  * allow per-variant price/discount.
  * allow active/inactive per variant.
  * provide "copy product variant base prices" action.
  * provide bulk edit if feasible.
* Show clear Persian labels:

  * قیمت فروش در این فروشگاه
  * قیمت تنوع در این فروشگاه
  * فعال در این فروشگاه
* Avoid mixing capacity in shop UI in v1.

Answer:

* Should AddEditShopProductDialog be expanded or replaced?
* Should there be a ShopProduct details page?
* Should product variants be editable inline?
* Should we keep v1 simple and only allow all variants with copied prices?

### 8. Customer WebApp Impact

Analyze product detail and variant selector.

Plan:

* Product detail should only show variants active in the selected shop.
* Prices displayed for variants should come from ShopProductVariant when available.
* If a variant is not enabled for the shop, it should not be shown or should be disabled.
* Cart add-to-cart must use backend price resolution, not WebApp-calculated price.
* WebApp should display shop-specific variant price to match backend.

Answer:

* Which DTOs must change?
* Should ProductVariantDto include shop-specific price fields?
* Should backend product detail projection join ShopProductVariant?
* How should variant selector handle missing ShopProductVariant?

### 9. API / DTO / Contract Plan

Plan backend contract changes:

* ShopProduct DTOs.
* Product detail DTOs.
* ProductVariantDto price fields.
* Add/update ShopProduct request.
* Add/update ShopProductVariant request.
* Admin API endpoints.
* Customer product detail endpoints.
* Cart DTO if needed.

Do not implement; only plan.

### 10. Migration / Data Compatibility Plan

Plan migration from current data.

Questions:

* Existing ShopProduct price exists for all shop-product mappings.
* Existing ProductVariant price exists globally.
* When introducing ShopProductVariant, how should initial rows be created?
* For each existing ShopProduct with product variants:

  * create ShopProductVariant for all active ProductVariants?
  * copy ProductVariant.PriceMinor?
  * copy ShopProduct price?
  * or require manual admin configuration?
* How to avoid breaking products immediately after migration?
* Should there be a background/backfill script?
* Should migration insert rows, or should admin action do it?
* How to handle products without variants?

### 11. Recommended Phases

Propose implementation phases after this planning.

At minimum:

* Phase 19: Backend domain/persistence/contracts for ShopProductVariant
* Phase 20: Admin ShopProductVariant UI
* Phase 21: Backend product detail/cart/checkout price resolution
* Phase 22: Customer WebApp variant price display
* Phase 23: Migration/backfill/manual QA
* Phase 24: Cleanup fallback behavior and release decision

For each phase include:

* Goal
* Files likely to change
* Validation commands
* Risks
* Manual QA requirements

## Strict Rules

Do not:

* Modify source files.
* Create migrations.
* Implement ShopProductVariant.
* Change Admin UI.
* Change WebApp.
* Change Cart/Checkout.
* Remove ShopProduct.
* Remove ProductVariant price.
* Add capacity override to shop variant unless strongly justified.
* Change Orders/Wallet.
* Mark production-ready.
* Ignore existing data migration.
* Ignore legacy non-variant products.

## Final Report Format

Save the report to:

```text id="jnwzts"
.codex/reports/05-phase-18-shopproductvariant-pricing-impact-plan.md
```

Use exactly this structure:

```text id="qjt9mx"
Summary:
- ...

Skills used:
- ...

Repositories inspected:
- ...

Git state:
- Backend:
  - ...
- Admin:
  - ...
- WebApp:
  - ...

Current ShopProduct model:
- ...

Current ProductVariant pricing model:
- ...

Product vs Variant shop offering options:
- ...

Recommended ShopProductVariant model:
- ...

Pricing resolution strategy:
- ...

Cart/checkout/order impact:
- ...

Admin UI/UX plan:
- ...

Customer WebApp impact:
- ...

API/DTO/contract plan:
- ...

Migration/data compatibility plan:
- ...

Recommended implementation phases:
- ...

Risks / assumptions:
- ...

Next implementation prompt:
- ...
```
