Summary:

  - Recommendation: use Option B. Shops continue adding Product through ShopProduct; products with variants get child
    ShopProductVariant rows for shop-specific variant availability and price.

  - Do not add shop-level stock/capacity in v1. Capacity remains on ProductVariant; Orders remains the only payable
    object.

  - In Plan Mode I did not save .codex/reports/05-phase-18-shopproductvariant-pricing-impact-plan.md; creating that
    file is a repo mutation.

  Skills used:

  - architecture-guardian
  - order-wallet-guardian
  - rendering-governance-expert
  - frontend-component-reuse
  - design-to-ui-implementation
  - module-owned-order-detail

  Repositories inspected:

  - Backend: C:\Workspace\repo\refahiplus-api
  - Admin: C:\Workspace\repo\refahiplus-admin
  - WebApp: C:\Workspace\repo\refahiplus-webapp

  Git state:

  - Backend:
      - Dirty. Store cart/variant/capacity/order files are modified, plus untracked Phase 16/18 prompt/report files
        and capacity query/service files.

  - Admin:
      - Dirty. CreateEditProductPage.razor and ProductSessionEditor.razor modified.

  - WebApp:
      - Dirty. Store cart/product detail/variant selector files modified, plus untracked .codex/.

  Current ShopProduct model:

  - ShopProduct is a Store domain aggregate-style mapping between ShopId and ProductId.
  - Fields: Id, ShopId, ProductId, Price, DiscountedPrice, Description, IsActive, IsDeleted, CreatedAt, UpdatedAt.
  - It owns shop-level product price, discount, description, active state, and soft-delete state.
  - It does not reference variants and does not affect ProductVariant.StockCount, ProductVariant.Capacity, validity
    dates, or usage date rules.

  - EF maps it to store.shop_products with filtered unique index on (ShopId, ProductId) where IsDeleted = false, plus
    indexes on ShopId, ProductId, IsDeleted.

  - APIs and handlers are product-level: add/update/remove/enable/disable/get by (shopId, productId).
  - Admin uses ShopProductsPage plus AddEditShopProductDialog; the dialog edits only product-level price/discount/
    description.

  - Customer product listing/detail uses ShopProduct to display product-level price, but variant display uses global
    ProductVariant price.

  Current ProductVariant pricing model:

  - ProductVariant stores PriceMinor, nullable DiscountedPriceMinor, StockCount, FromDate, ToDate, CapacityType,
    Capacity, and availability logic.

  - ProductVariantDto exposes global variant price/discount and capacity/validity metadata.
  - Backend product detail projects ShopProduct.Price/DiscountedPrice as product price and
    ProductVariant.PriceMinor/DiscountedPriceMinor as variant price.

  - Add-to-cart and sync currently use ShopProduct price for non-variant items and ProductVariant price for variant
    items.

  - Cart stores CartItem.UnitPriceMinor; checkout passes that snapshot to Orders as OrderItem.UnitPriceMinor.
  - Orders stores price first-class in OrderItem.UnitPriceMinor/FinalPriceMinor; variant/session/shop IDs live in
    MetadataJson.

  Product vs Variant shop offering options:

  - Option A, Product only: lowest migration risk but cannot support shop-specific variant price or availability; not
    enough for the new sellable-option model.

  - Option B, Product parent plus selected variants: safest. Keeps existing ShopProduct compatibility and adds precise
    variant offering rows.

  - Option C, Variant direct listing: too disruptive; would rewrite admin UX, product discovery, cart, historical
    data, and existing ShopProduct semantics.

  - Recommended: Option B.

  Recommended ShopProductVariant model:

  - Add under Store Domain as a child entity/concept of ShopProduct, persisted in store.shop_product_variants.
  - V1 fields: Id, ShopProductId, ProductVariantId, PriceMinor, DiscountedPriceMinor, IsActive, IsDeleted, CreatedAt,
    UpdatedAt.

  - Use nullable DiscountedPriceMinor or require positive discount consistently; recommended nullable to match
    ProductVariant and avoid fake discount values.

  - Do not include description, display order, stock override, capacity override, or validity override in v1.
  - Uniqueness: one non-deleted row per (ShopProductId, ProductVariantId).
  - Indexes: ShopProductId, ProductVariantId, IsDeleted, filtered unique (ShopProductId, ProductVariantId) where not
    deleted.

  - Repository should support get by (shopId, productId, variantId) through join or ShopProductId, and batch lookup
    for product detail/list display.

  Pricing resolution strategy:

  - If no VariantId: authoritative price is ShopProduct.DiscountedPrice > 0 ? DiscountedPrice : Price.
  - If VariantId: authoritative price is active ShopProductVariant.DiscountedPriceMinor ?? PriceMinor.
  - During migration only, allow fallback to ProductVariant.DiscountedPriceMinor ?? PriceMinor when no active
    ShopProductVariant exists.

  - After backfill/manual QA, remove fallback and reject variant add-to-cart when active ShopProductVariant is
    missing.

  - ShopProduct price remains meaningful for non-variant products and parent listing display; it should not price
    variant purchases after rollout.

  - ProductVariant price becomes global/base/reference price for copying and fallback, not the final shop sale price
    for variant products.

  Cart/checkout/order impact:

  - Add-to-cart must validate active ShopProduct first, then active ShopProductVariant when VariantId is supplied.
  - SyncCart must use the same resolver and emit PRICE_CHANGED when local snapshot differs from resolved shop-variant
    price.

  - PlaceStoreOrder should re-resolve authoritative price before creating Orders, not blindly trust
    CartItem.UnitPriceMinor; if changed, either update/reject with Persian error.

  - OrderItem.MetadataJson should add shop_product_variant_id while keeping variant_id, shop_id, usage_date, capacity
    metadata, and existing fields.

  - Capacity and UsageDate continue to use ProductVariant; shop variant only gates availability and price.
  - If ShopProduct is inactive/deleted, variant is unavailable even if ShopProductVariant is active.
  - If variant is active globally but not enabled for the shop, hide/disable it on product detail and reject add-to-
    cart.

  Admin UI/UX plan:

  - Keep ShopProductsPage as the parent product listing.
  - Expand or replace AddEditShopProductDialog with a larger dialog/details surface because per-variant rows will not
    fit the current compact form well.

  - For products without variants: keep existing fields labeled قیمت فروش در این فروشگاه.
  - For products with variants: show variant list, selection checkbox/toggle, قیمت تنوع در این فروشگاه, discount, and
    فعال در این فروشگاه.

  - Add “copy base variant prices” behavior from ProductVariant prices; bulk edit can be deferred if the first
    implementation becomes too large.

  - Do not expose capacity/validity override in shop UI v1.

  Customer WebApp impact:

  - Product detail should show only variants active for the selected shop, or disabled unavailable variants if UX
    requires visibility.

  - Variant displayed price must come from ShopProductVariant; product-level price remains the default for non-variant
    products.

  - ProductVariantDto should add shop-specific offering fields or be replaced by a shop-aware variant DTO:
    ShopProductVariantId, shop price, shop discounted price, shop active/available.

  - ProductPurchaseIsland should use the displayed shop-variant price for anonymous local cart snapshots, but backend
    remains authoritative.

  - No render-mode change is needed for product detail; it is SSR with WASM islands using prerender: false.

  API/DTO/contract plan:

  - Add Store contracts for shop product variants: list/upsert/enable/disable/remove under admin shop-product
    endpoints.

  - Extend ShopProductDto with variant summary/count/status only if needed for admin table.
  - Extend admin product detail or add a shop-product detail endpoint that returns product variants plus existing
    shop-specific variant rows.

  - Extend public product detail projection to accept/derive shop context and return shop-filtered variant offerings.
  - Add a shared price resolver service in Store Application to avoid duplicating price precedence in AddToCart,
    SyncCart, GetCart, PlaceStoreOrder, and product detail.

  - Do not change Orders/Wallet contracts except adding Store-owned metadata content.

  Migration/data compatibility plan:

  - Products without variants: no migration needed; continue using ShopProduct price.
  - Existing ShopProduct with variants: backfill ShopProductVariant rows for all active ProductVariant rows.
  - Backfill price source: copy ProductVariant.PriceMinor/DiscountedPriceMinor, not ShopProduct price, because
    existing variant checkout already uses variant price.

  - Keep temporary fallback to global variant price to avoid immediate breakage if backfill misses rows.
  - Add an admin audit/report for products with variants and missing shop variant rows.
  - Remove fallback only after production data is confirmed backfilled and admin can manage variant offerings.

  Recommended implementation phases:

  - Phase 19 Backend domain/persistence/contracts: add entity, DbSet/configuration/repository/contracts/endpoints, no
    behavior switch yet. Validate with dotnet build and Store tests.

  - Phase 20 Admin UI: add shop variant management surface, copy-base-prices action, Persian labels, validation.
    Validate manually on products with and without variants.

  - Phase 21 Backend price resolution: central resolver, update product detail/add/sync/cart/checkout, metadata add
    shop_product_variant_id. Validate add-to-cart, sync, checkout, Orders snapshot.

  - Phase 22 WebApp display: extend DTOs/view models/selector/purchase island to show shop-specific variant prices and
    availability. Validate SSR detail plus WASM island behavior.

  - Phase 23 Migration/backfill/QA: create migration/backfill script, verify counts and price parity with current
    behavior.

  - Phase 24 Cleanup: remove fallback, enforce active ShopProductVariant for variant cart, document release decision.

  - Current dirty changes are treated as in-progress user work; the plan relies on observed current files, not a
    committed baseline.

  - Backend product detail currently lacks explicit shop context and chooses the first active ShopProduct; this must
    be corrected for shop-specific variant pricing.

  - Cart currently snapshots price at add/sync time and checkout does not reprice; this is a correctness risk that
    Phase 21 must fix.

  - DiscountedPrice semantics differ: ShopProduct uses required positive value, ProductVariant uses nullable discount.
    New model should align with nullable discount.

  - Daily deals and featured products remain product-level in v1 unless the business wants variant-specific
    promotions.

  Next implementation prompt:

  - “Implement Phase 19 only: introduce backend ShopProductVariant domain/persistence/contracts/admin endpoints in
    Store without changing cart/checkout pricing behavior. Preserve existing ShopProduct behavior, add migration/
    backfill skeleton only if explicitly requested, and do not touch Admin/WebApp yet.”


─ Worked for 6m 49s ────────────────────────────────────────────────────────────────────────────────────────────────────

  Implement this plan?

› 1. Yes, implement this plan          Switch to Default and start coding.
  2. Yes, clear context and implement  Fresh thread. Context: 80% used.
  3. No, stay in Plan mode             Continue planning with the model.

  Press enter to confirm or esc to go back      