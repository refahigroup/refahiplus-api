Summary:
- Added explicit shop context support to public Store product detail retrieval while preserving the existing no-context product detail route behavior.
- Product detail can now receive optional `shopSlug` and `shopId` query parameters and resolve prices from the exact requested shop product.
- Existing `ShopProduct`, `ShopProductVariant`, `ProductVariant.PriceMinor`, cart, checkout, Orders, Wallet, Admin Web, and Customer WebApp behavior were left unchanged.

Skills used:
- `.codex/skills/architecture-guardian.md`
- `.codex/skills/order-wallet-guardian.md`
- `.codex/skills/rendering-governance-expert.md`

Repository checked:
- `C:\Workspace\repo\refahiplus-api`

Git state:
- Pre-check:
  - Repository was dirty before edits.
  - Pre-existing modified files included Store cart/checkout/runtime pricing files and `GetProductBySlugQueryHandler.cs`.
  - Pre-existing untracked files included Phase 21/23 prompt/report files and Store price resolver service files.
- Final:
  - Repository remains dirty.
  - Phase 23 touched `GetProductBySlugEndpoint.cs`, `GetProductBySlugQuery.cs`, `GetProductBySlugQueryHandler.cs`, and this report.
  - Other dirty files were preserved as existing work.

Files changed:
- `src/Refahi.Modules.Store.Api/Endpoints/Products/GetProductBySlugEndpoint.cs`
- `src/Refahi.Modules.Store.Application.Contracts/Queries/Products/GetProductBySlugQuery.cs`
- `src/Refahi.Modules.Store.Application/Features/Products/GetProductBySlug/GetProductBySlugQueryHandler.cs`
- `.codex/reports/phase-23-backend-product-detail-explicit-shop-context.md`

API contract changes:
- Existing route is preserved: `GET /api/store/{moduleSlug}/products/{slug}`.
- Added optional query parameters: `shopSlug` and `shopId`.
- Supported examples:
  - `GET /api/store/{moduleSlug}/products/{slug}?shopSlug={shopSlug}`
  - `GET /api/store/{moduleSlug}/products/{slug}?shopId={shopId}`
  - `GET /api/store/{moduleSlug}/products/{slug}?shopSlug={shopSlug}&shopId={shopId}`

Query/application changes:
- Extended `GetProductBySlugQuery` to carry optional `ShopSlug` and `ShopId` while keeping the original slug-only constructor shape valid through default parameter values.
- `GetProductBySlugQueryHandler` now injects `IShopRepository` and uses existing Store repositories instead of adding new persistence methods.
- Admin product detail query was not changed.

Shop context resolution:
- With `shopSlug`, the handler resolves an active shop by slug, then loads that shop's non-deleted `ShopProduct` with variant offerings.
- With `shopId`, the handler verifies the shop exists and is active, then loads that shop's non-deleted `ShopProduct` with variant offerings.
- With both values, the handler rejects mismatched context with the Persian message `اطلاعات فروشگاه با درخواست محصول هم‌خوانی ندارد.`
- If the product is not available through the requested active shop product, public product detail returns not found through existing endpoint behavior.
- Without shop context, the handler keeps the legacy first-active-`ShopProduct` fallback and labels it in code.

Product-level price behavior:
- Explicit shop context uses only that shop's `ShopProduct.Price` and `ShopProduct.DiscountedPrice`.
- No explicit context keeps the legacy first-active shop product pricing fallback.
- The handler does not show another shop's price when explicit shop context is supplied.

Variant price behavior:
- Active `ShopProductVariant` offerings provide variant price fields for the requested shop and mark `UsesShopSpecificPrice = true`.
- Inactive non-deleted `ShopProductVariant` offerings remain in the projection with `IsActiveInShop = false`, making the variant unavailable through the public DTO.
- Missing `ShopProductVariant` offerings continue to use the temporary `ProductVariant` fallback and mark `PriceSource = ProductVariantFallback`.
- Existing Phase 21 fields are preserved: `ShopProductVariantId`, `ShopPriceMinor`, `ShopDiscountedPriceMinor`, `PriceSource`, `IsActiveInShop`, and `UsesShopSpecificPrice`.

Backward compatibility:
- Old product detail calls without shop context still compile and run.
- Existing route compatibility is preserved.
- Runtime ProductVariant price fallback is preserved.
- Cart, checkout, Orders, Wallet, Admin Web, Customer WebApp, migrations, backfill, and package versions were not changed.

Validation:
- Command: `dotnet restore .\Refahi.Backend.slnx`
  Result: Passed. Existing NU1903 warnings remain for `System.Security.Cryptography.Xml` in PaymentGateway infrastructure.
- Command: `dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore`
  Result: Passed. Existing warnings remain in PaymentGateway, Store `ShopProductsEndpoints`, `Refahi.Api`, and version metadata.
- Command: `dotnet test .\Refahi.Backend.slnx --configuration Release --no-build`
  Result: Passed. 36 tests passed across Flights and PaymentGateway test assemblies.

Manual QA:
- Not run. No local API/test data workflow was exercised in this phase.

Deferred items:
- Focused Store automated tests were not added because the repository currently has no Store test project; introducing new test infrastructure was out of scope.
- Customer WebApp must still pass `shopSlug` or `shopId` to use the explicit backend context.
- Backfill and production readiness remain out of scope.

Risks / assumptions:
- `shopSlug` matching follows the existing Store slug repository convention and normalizes input with trim/lowercase in the handler.
- `shopId` support assumes public callers can safely pass the Store shop ID when available.
- Mismatch rejection relies on existing API exception middleware handling `ArgumentException` as a bad request.
- The legacy no-context fallback remains intentionally non-deterministic across multiple active shop products beyond the existing repository ordering.

Suggested next phase:
- Update Customer WebApp product detail calls to pass the route shop slug as `shopSlug`, then add focused end-to-end/API QA for two shops carrying the same product with different variant offering prices.
