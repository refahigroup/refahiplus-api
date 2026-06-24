Summary:
- Added centralized Store runtime price resolution for `ShopProduct` and `ShopProductVariant`.
- Updated cart add/sync/get/update and Store checkout to use resolved authoritative prices.
- Preserved temporary fallback to `ProductVariant` price when no non-deleted shop variant offering exists.
- Kept Admin Web, Customer WebApp, Docker, GitHub Actions, wallet flow, and Orders contracts unchanged.

Skills used:
- `.codex/skills/architecture-guardian.md`
- `.codex/skills/order-wallet-guardian.md`
- `.codex/skills/module-owned-order-detail.md`
- `C:\Workspace\Refahi\skills\architecture-guardian.md`
- `C:\Workspace\Refahi\skills\order-wallet-guardian.md`

Repository checked:
- `C:\Workspace\repo\refahiplus-api`

Git state:
- Pre-check: dirty; untracked `.codex/prompts/07-Phase-21-backend-shopproductvariant-runtime-pricing.md` existed before changes.
- Final: modified Store backend files and new Store price resolver files; original untracked prompt remains untouched.

Files changed:
- `src/Refahi.Modules.Store.Application/Services/IStoreProductPriceResolver.cs`
- `src/Refahi.Modules.Store.Application/Services/StoreProductPriceResolver.cs`
- `src/Refahi.Modules.Store.Application/Services/StoreResolvedPrice.cs`
- `src/Refahi.Modules.Store.Application/DI.cs`
- `src/Refahi.Modules.Store.Application/Features/Cart/AddToCart/AddToCartCommandHandler.cs`
- `src/Refahi.Modules.Store.Application/Features/Cart/SyncCart/SyncCartCommandHandler.cs`
- `src/Refahi.Modules.Store.Application/Features/Cart/GetCart/GetCartQueryHandler.cs`
- `src/Refahi.Modules.Store.Application/Features/Cart/UpdateCartItem/UpdateCartItemCommandHandler.cs`
- `src/Refahi.Modules.Store.Application/Features/Checkout/PlaceStoreOrder/PlaceStoreOrderCommandHandler.cs`
- `src/Refahi.Modules.Store.Application/Features/Products/GetProductBySlug/GetProductBySlugQueryHandler.cs`
- `src/Refahi.Modules.Store.Application.Contracts/Dtos/Cart/CartDto.cs`
- `src/Refahi.Modules.Store.Application.Contracts/Dtos/Products/ProductVariantDto.cs`
- `src/Refahi.Modules.Store.Domain/Aggregates/Cart.cs`
- `src/Refahi.Modules.Store.Domain/Entities/CartItem.cs`
- `.codex/reports/phase-21-backend-shopproductvariant-runtime-pricing.md`

Price resolver:
- Added `StoreProductPriceResolver` with `StoreResolvedPrice` and `StorePriceSource`.
- Non-variant prices resolve from active, non-deleted `ShopProduct`.
- Variant prices resolve from active, non-deleted `ShopProductVariant`.
- Inactive non-deleted `ShopProductVariant` is rejected with Persian error `این تنوع در فروشگاه انتخاب‌شده فعال نیست.`.
- Missing non-deleted `ShopProductVariant` falls back to `ProductVariant` price and logs fallback usage.
- Price validation enforces price `> 0` and discounted price `> 0` and `< price`.

Repository changes:
- No new repository methods were needed.
- Existing `IShopProductRepository.GetWithVariantOfferingsAsync` is used by the resolver.
- EF queries were not scattered into runtime handlers.

AddToCart pricing:
- `AddToCartCommandHandler` now uses the resolver for runtime unit price.
- Stock-based non-variant products continue using `ShopProduct` price.
- Stock-based variant products use active `ShopProductVariant` price or fallback to `ProductVariant`.
- Session-based legacy `SessionId` items use `ShopProduct` price plus session adjustment.
- Session-based variant-capacity items use shop variant pricing or fallback while preserving usage-date/capacity checks.

SyncCart pricing:
- `SyncCartCommandHandler` re-resolves authoritative price for each incoming item.
- `PRICE_CHANGED` warning remains when local snapshot price differs from resolved price.
- Inactive shop variant offerings are dropped through the existing warning flow as `VARIANT_REMOVED`.
- Missing shop variant offerings continue through ProductVariant fallback.

GetCart pricing/projection:
- `GetCartQueryHandler` now resolves current price/source for each item where possible.
- Added optional `CurrentUnitPriceMinor`, `HasPriceChanged`, `ShopProductVariantId`, and `PriceSource` fields to `CartItemDto`.
- Existing snapshot `UnitPriceMinor` remains unchanged in read projection.

UpdateCartItem behavior:
- Quantity validation remains stock/capacity based.
- Successful quantity update now re-resolves unit price and updates the cart item snapshot.
- Legacy session-row price adjustment is preserved.

PlaceStoreOrder repricing:
- `PlaceStoreOrderCommandHandler` re-resolves authoritative unit price before creating the Order.
- If resolved unit price differs from cart snapshot, order creation is rejected with Persian refresh-cart error.
- Orders are still created only through the Orders module; Wallet flow remains untouched.

Order metadata:
- Existing metadata fields are preserved.
- Added `shop_product_variant_id` when resolver returns one.
- Added `price_source` metadata for traceability.
- Existing `variant_id`, `session_id`, and `usage_date` behavior is preserved.

Product detail projection:
- Public `GetProductBySlug` variant projection now exposes shop-specific variant price fields when a shop product context exists.
- Missing offering rows return fallback ProductVariant pricing.
- Inactive shop variant offerings mark the variant inactive for the shop.
- Existing `ProductVariantDto.PriceMinor` and `DiscountedPriceMinor` remain base values for Admin because `AdminGetProduct` was not changed to replace them.

Backward compatibility:
- No Admin Web or Customer WebApp source was modified.
- Existing Store API routes and Orders contracts remain unchanged.
- Existing `ShopProduct`, `ProductVariant.PriceMinor`, `ProductVariant.StockCount`, product sessions, and fallback behavior remain.
- No shop-level stock/capacity override, reservation, or ledger was introduced.

Validation:
- Command: `dotnet restore .\Refahi.Backend.slnx`
  Result: Passed; existing `NU1903` warnings for `System.Security.Cryptography.Xml` in PaymentGateway Infrastructure.
- Command: `dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore`
  Result: Passed; final run had 9 warnings, including existing PaymentGateway `NU1903` and nullable/version warnings outside this task.
- Command: `dotnet test .\Refahi.Backend.slnx --configuration Release --no-build`
  Result: Passed; 36 tests passed across existing Flights and PaymentGateway test projects.

Manual QA:
- Not run; no local API/auth/test data was used in this phase.

Fallback behavior:
- Required temporary fallback remains active for variant purchases with no non-deleted `ShopProductVariant` row.
- Deleted offering rows are ignored, so if no non-deleted row exists the resolver falls back to `ProductVariant`.
- Existing non-deleted inactive offerings are rejected and do not fallback.

Deferred items:
- No backfill for `ShopProductVariant` rows.
- No fallback removal.
- No Store-specific automated tests were added because no Store test project exists in the current test tree.
- No manual API QA was performed.

Risks / assumptions:
- Public product detail still has no explicit shop id in `GetProductBySlugQuery`; it uses the existing first active `ShopProduct` behavior as the shop context.
- Existing build warnings remain outside this phase.
- Fallback logging may be noisy until shop variant offering backfill is completed.

Suggested next phase:
- Backfill and QA `ShopProductVariant` rows for all variant offerings, then remove ProductVariant fallback and reject missing shop variant offerings.
