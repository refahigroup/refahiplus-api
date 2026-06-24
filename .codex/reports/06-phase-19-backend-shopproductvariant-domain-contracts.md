Summary:
- Implemented backend support for admin-managed ShopProductVariant offerings under Store without changing cart, checkout, product detail, Orders, Wallet, Admin Web, or Customer WebApp behavior.

Skills used:
- architecture-guardian
- order-wallet-guardian

Repository checked:
- C:\Workspace\repo\refahiplus-api

Git state:
- Pre-check: repository had one existing untracked prompt file: `.codex/prompts/06-phase-19-backend-shopproductvariant-domain-contracts.md`.
- Final: Store backend files, generated Store migration/snapshot, and this report are changed/untracked; the existing untracked prompt file remains preserved.

Files changed:
- `src/Refahi.Modules.Store.Domain/Aggregates/ShopProduct.cs`
- `src/Refahi.Modules.Store.Domain/Entities/ShopProductVariant.cs`
- `src/Refahi.Modules.Store.Domain/Repositories/IShopProductRepository.cs`
- `src/Refahi.Modules.Store.Application.Contracts/Dtos/ShopProducts/ShopProductVariantDto.cs`
- `src/Refahi.Modules.Store.Application.Contracts/Queries/ShopProducts/ListShopProductVariantsQuery.cs`
- `src/Refahi.Modules.Store.Application.Contracts/Commands/ShopProducts/UpsertShopProductVariantCommand.cs`
- `src/Refahi.Modules.Store.Application.Contracts/Commands/ShopProducts/ManageShopProductVariantCommands.cs`
- `src/Refahi.Modules.Store.Application/Features/ShopProducts/ShopProductVariants/*`
- `src/Refahi.Modules.Store.Api/Endpoints/ShopProducts/ShopProductsEndpoints.cs`
- `src/Refahi.Modules.Store.Infrastructure/Persistence/Context/StoreDbContext.cs`
- `src/Refahi.Modules.Store.Infrastructure/Persistence/Configurations/ShopProductConfiguration.cs`
- `src/Refahi.Modules.Store.Infrastructure/Persistence/Configurations/ShopProductVariantConfiguration.cs`
- `src/Refahi.Modules.Store.Infrastructure/Repositories/ShopProductRepository.cs`
- `src/Refahi.Modules.Store.Infrastructure/Migrations/20260624103955_Store_AddShopProductVariants.cs`
- `src/Refahi.Modules.Store.Infrastructure/Migrations/20260624103955_Store_AddShopProductVariants.Designer.cs`
- `src/Refahi.Modules.Store.Infrastructure/Migrations/StoreDbContextModelSnapshot.cs`
- `.codex/reports/06-phase-19-backend-shopproductvariant-domain-contracts.md`

Domain changes:
- Added `ShopProductVariant` with shop-product parent ID, product-variant ID, shop-specific price, nullable discounted price, active flag, soft-delete flag, and timestamps.
- Added `ShopProduct` aggregate methods for add, update, enable, disable, and soft-delete of variant offerings.
- Centralized price validation and duplicate non-deleted offering prevention in domain behavior.

Persistence / migration:
- Added `DbSet<ShopProductVariant>`.
- Added EF mapping for `store.shop_product_variants`.
- Added FKs to `store.shop_products` and `store.product_variants` with restrict delete behavior.
- Added indexes on `ShopProductId`, `ProductVariantId`, `IsDeleted`, and filtered unique index on `(ShopProductId, ProductVariantId)` where `IsDeleted = false`.
- Generated migration `20260624103955_Store_AddShopProductVariants`; no migration was applied to a database.

Repository changes:
- Added `GetWithVariantOfferingsAsync` to load a `ShopProduct` aggregate with variant offerings.
- Updated `UpdateAsync` to avoid re-attaching already tracked aggregates, preserving EF child-add tracking.

Application contracts:
- Added `ShopProductVariantDto`.
- Added `ListShopProductVariantsQuery`.
- Added `UpsertShopProductVariantCommand`.
- Added enable, disable, and remove commands for shop product variants.

Application handlers:
- Added list, upsert, enable, disable, and remove handlers.
- Upsert validates that the parent shop product exists, the product exists and is not deleted, the variant belongs to that product, and the variant is available.
- Upsert creates or updates a non-deleted offering without changing `ProductVariant.PriceMinor`, stock, capacity, cart, checkout, or public price resolution.

API endpoints:
- Added `GET /admin/shops/{shopId}/products/{productId}/variants`.
- Added `PUT /admin/shops/{shopId}/products/{productId}/variants/{variantId}`.
- Added `PATCH /admin/shops/{shopId}/products/{productId}/variants/{variantId}/enable`.
- Added `PATCH /admin/shops/{shopId}/products/{productId}/variants/{variantId}/disable`.
- Added `DELETE /admin/shops/{shopId}/products/{productId}/variants/{variantId}`.
- All new endpoints use `AdminOnly`, `ApiResponseHelper`, `WithName()`, and `WithTags()`.

Backward compatibility:
- Existing ShopProduct APIs and DTOs were not removed or renamed.
- Existing `ShopProduct.Price` / `DiscountedPrice` behavior remains unchanged.
- Existing `ProductVariant.PriceMinor`, stock, capacity, cart, checkout, and product detail price resolution remain unchanged.
- No Orders or Wallet code was changed.
- No Admin Web or Customer WebApp code was changed.

Validation:
- Command: `dotnet restore .\Refahi.Backend.slnx`
  Result: Passed. Existing NU1903 warnings reported for `System.Security.Cryptography.Xml` 9.0.0 in `Refahi.Modules.PaymentGateway.Infrastructure`.
- Command: `dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore`
  Result: Passed. Final build reported 5 warnings: 2 existing NU1903 PaymentGateway package warnings and 3 existing nullable warnings in pre-existing ShopProduct endpoint `Success<object>(null)` calls.
- Command: `dotnet test .\Refahi.Backend.slnx --configuration Release --no-build`
  Result: Passed. Flights tests: 11 passed. PaymentGateway tests: 25 passed.
- Command: `git diff --check`
  Result: Passed with line-ending normalization warnings only.

Manual QA:
- Not run. Local API/auth manual workflow was not exercised.
- No Store test project exists under `tests`; new test project/framework was not introduced.

Deferred items:
- Runtime cart/checkout price resolver changes.
- Public product detail variant price display changes.
- Backfill of shop product variant rows.
- Order metadata changes for shop product variant IDs.
- Admin Web UI changes.
- Customer WebApp changes.

Risks / assumptions:
- `ProductVariant` currently has `IsAvailable` but no soft-delete flag; validation uses `IsAvailable` and product ownership.
- Variant name is derived from existing variant attribute combinations, with SKU/fallback text when no combinations exist.
- New enable/disable variant endpoints use `PATCH` to match current Store ShopProduct endpoint style.

Suggested next phase:
- Add Store-focused tests when a Store test project exists, then implement controlled runtime price resolution for variant offerings with backfill and Admin/WebApp UI updates.
