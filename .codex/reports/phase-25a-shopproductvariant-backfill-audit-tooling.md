Summary:
- Added backend-only controlled audit and backfill tooling for missing Store ShopProductVariant offerings.
- Added admin-only API endpoints for audit and dry-run/write backfill.
- Backfill uses ProductVariant.PriceMinor and ProductVariant.DiscountedPriceMinor, writes only missing non-deleted offerings, and leaves existing configured offerings unchanged.

Skills used:
- architecture-guardian
- order-wallet-guardian

Repository checked:
- C:\Workspace\repo\refahiplus-api

Git state:
- Pre-check: one untracked prompt file existed: .codex/prompts/09-Phase-25a-shopproductvariant-backfill-audit-tooling.md
- Final: Store backend files changed and this report added; the untracked prompt file remains untouched.

Files changed:
- src/Refahi.Modules.Store.Application.Contracts/Queries/ShopProducts/GetShopProductVariantBackfillAuditQuery.cs
- src/Refahi.Modules.Store.Application.Contracts/Commands/ShopProducts/BackfillShopProductVariantsCommand.cs
- src/Refahi.Modules.Store.Application/Features/ShopProducts/ShopProductVariantBackfill/GetShopProductVariantBackfillAuditQueryHandler.cs
- src/Refahi.Modules.Store.Application/Features/ShopProducts/ShopProductVariantBackfill/BackfillShopProductVariantsCommandHandler.cs
- src/Refahi.Modules.Store.Application/Features/ShopProducts/ShopProductVariantBackfill/BackfillShopProductVariantsCommandValidator.cs
- src/Refahi.Modules.Store.Api/Endpoints/ShopProducts/ShopProductsEndpoints.cs
- src/Refahi.Modules.Store.Domain/Repositories/IShopProductRepository.cs
- src/Refahi.Modules.Store.Domain/Repositories/IProductRepository.cs
- src/Refahi.Modules.Store.Domain/Repositories/IShopRepository.cs
- src/Refahi.Modules.Store.Infrastructure/Repositories/ShopProductRepository.cs
- src/Refahi.Modules.Store.Infrastructure/Repositories/ProductRepository.cs
- src/Refahi.Modules.Store.Infrastructure/Repositories/ShopRepository.cs
- .codex/reports/phase-25a-shopproductvariant-backfill-audit-tooling.md

Audit contract/API:
- Added GetShopProductVariantBackfillAuditQuery with optional ShopId, ProductId, and capped DetailLimit.
- Added ShopProductVariantBackfillAuditDto and item DTO with checked counts, products with variants, existing offerings, missing offerings, and first-N missing detail rows.
- Added GET /admin/shop-product-variants/backfill/audit with AdminOnly authorization and ApiResponseHelper wrapping.

Backfill contract/API:
- Added BackfillShopProductVariantsCommand with optional ShopId, ProductId, DryRun defaulting to true, and capped DetailLimit.
- Added ShopProductVariantBackfillResultDto with dry-run/write metadata, created count, skipped existing count, skipped invalid count, preview/created items, and warnings.
- Added POST /admin/shop-product-variants/backfill with AdminOnly authorization and ApiResponseHelper wrapping.

Backfill behavior:
- Processes non-deleted ShopProduct rows matching optional filters.
- Skips missing/deleted Products safely.
- Ignores Products without variants.
- For each missing ProductVariant offering, uses ProductVariant.PriceMinor and ProductVariant.DiscountedPriceMinor.
- Creates new ShopProductVariant rows as active only when DryRun is false.
- Skips ProductVariants with invalid base price or invalid discounted price and returns warnings.

Safety/idempotency:
- Existing non-deleted ShopProductVariant offerings, including inactive offerings, are treated as existing and are never overwritten.
- Soft-deleted offerings do not block creating a new non-deleted offering.
- The existing filtered unique index on ShopProductId/ProductVariantId for non-deleted rows remains the database idempotency guard.
- Re-running write mode after a successful write should create zero additional rows.

Repository/persistence changes:
- Added IShopProductRepository.ListForVariantBackfillAsync to load non-deleted ShopProducts with variant offerings.
- Added IProductRepository.GetByIdsForAdminWithDetailsAsync to batch-load Products with variants and variant metadata.
- Added IShopRepository.GetByIdsAsync to batch-load shop names for audit/backfill responses.
- No EF migration was added and no schema changes were made.

Backward compatibility:
- Runtime StoreProductPriceResolver fallback remains active.
- ProductVariant.PriceMinor and ShopProduct.Price were not removed or changed.
- Existing cart, checkout, product detail, Orders, and Wallet behavior were not changed.
- Admin Web and Customer WebApp source were not modified.

Validation:
- Command: dotnet restore .\Refahi.Backend.slnx
  Result: Passed; existing PaymentGateway System.Security.Cryptography.Xml 9.0.0 NU1903 advisories were reported.
- Command: dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore
  Result: Passed with warnings only; warnings are existing nullable/version/advisory warnings plus existing Store endpoint null wrapper warnings.
- Command: dotnet test .\Refahi.Backend.slnx --configuration Release --no-build
  Result: Passed; 36 tests passed across existing Flights and PaymentGateway test projects.

Manual QA:
- Not run. No local API/auth/test data flow was exercised.

Fallback behavior:
- ProductVariant runtime fallback remains active and unchanged.
- No actual database backfill was executed.

Deferred items:
- No Store-specific test project currently exists, so focused Store backfill tests were not added in this phase.
- Production execution of the write-mode backfill remains a separate operational decision.

Risks / assumptions:
- The tooling is intended for controlled admin use; very large datasets may need paging/batching beyond the current capped response details.
- Deleted products are skipped; inactive non-deleted ShopProducts and Products are included only insofar as the non-deleted ShopProduct row is processed and the Product is not deleted.
- Write-mode concurrency still relies on the domain duplicate check plus the existing filtered unique index.

Suggested next phase:
- Run audit in a controlled environment, review warnings, execute dry-run for scoped ShopId/ProductId samples, then run write-mode backfill under an operational plan before removing runtime fallback in a later phase.
