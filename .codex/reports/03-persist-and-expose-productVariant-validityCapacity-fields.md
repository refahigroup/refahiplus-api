Summary:
- Persisted and exposed Store ProductVariant validity/capacity fields through backend EF mapping, migration, contracts, command handling, validation, and detail projections.
- Used the existing Phase 7 domain fields already present in the working tree and did not modify Admin UI, Customer WebApp, Cart, Checkout, or ProductSession flows.

Skills used:
- architecture-guardian
- order-wallet-guardian

Repository checked:
- C:\Workspace\repo\refahiplus-api
- Requested prompt path `./condex/prompts/03-persist-and-expose-productVariant-validityCapacity-fields.md` was missing; used existing `.codex/prompts/03-persist-and-expose-productVariant-validityCapacity-fields.md`.

Git state:
- Pre-check: `M src/Refahi.Modules.Store.Domain/Aggregates/Product.cs`; `M src/Refahi.Modules.Store.Domain/Entities/ProductVariant.cs`; `?? .codex/`; `?? src/Refahi.Modules.Store.Domain/Enums/VariantCapacityType.cs`.
- Final: Store backend contract/application/infrastructure files changed, Store migration files added, required report added under `.codex/reports/`; pre-existing domain changes and untracked `.codex/` remain present.

Files changed:
- src/Refahi.Modules.Store.Application.Contracts/Commands/Products/AddProductVariantCommand.cs
- src/Refahi.Modules.Store.Application.Contracts/Dtos/Products/ProductVariantDto.cs
- src/Refahi.Modules.Store.Application/Features/Products/AddProductVariant/AddProductVariantCommandHandler.cs
- src/Refahi.Modules.Store.Application/Features/Products/AddProductVariant/AddProductVariantCommandValidator.cs
- src/Refahi.Modules.Store.Application/Features/Products/AdminGetProduct/AdminGetProductQueryHandler.cs
- src/Refahi.Modules.Store.Application/Features/Products/GetProductBySlug/GetProductBySlugQueryHandler.cs
- src/Refahi.Modules.Store.Infrastructure/Persistence/Configurations/ProductVariantConfiguration.cs
- src/Refahi.Modules.Store.Infrastructure/Migrations/20260621153532_AddProductVariantValidityCapacity.cs
- src/Refahi.Modules.Store.Infrastructure/Migrations/20260621153532_AddProductVariantValidityCapacity.Designer.cs
- src/Refahi.Modules.Store.Infrastructure/Migrations/StoreDbContextModelSnapshot.cs
- .codex/reports/03-persist-and-expose-productVariant-validityCapacity-fields.md

Persistence changes:
- Mapped `FromDate` and `ToDate` as nullable `date` columns.
- Mapped `CapacityType` as required `smallint` with default `Unlimited` / `0`.
- Mapped `Capacity` as nullable `integer`.
- Added indexes on `(ProductId, CapacityType)` and `(ProductId, FromDate, ToDate)`.
- Kept existing Store column naming convention, so generated columns are `FromDate`, `ToDate`, `CapacityType`, and `Capacity`.

Migration:
- Added Store migration `20260621153532_AddProductVariantValidityCapacity`.
- Migration adds nullable validity dates, non-null capacity type with default 0, nullable capacity, and the two low-risk indexes.
- Model snapshot updated by EF.
- Did not apply the migration to a database.
- Did not remove or alter `product_sessions`.

Contract/DTO changes:
- `ProductVariantDto` now exposes `FromDate`, `ToDate`, `CapacityType`, `Capacity`, and `RequiresUsageDate`.
- `AddProductVariantCommand` now accepts `FromDate`, `ToDate`, `CapacityType`, and `Capacity` with backward-compatible defaults.

Command/validator changes:
- Add variant handler now passes validity/capacity fields into `Product.AddVariant(...)`.
- Added `AddProductVariantCommandValidator` with Persian validation messages for product id, stock, price, discounted price, validity range, enum value, and required positive capacity for `TotalPeriod` / `PerEligibleDay`.
- For `Unlimited`, incoming capacity is allowed and the domain normalizes it to null, preserving backward compatibility.

Query/projection changes:
- Public product detail variant projection returns the new fields.
- Admin product detail variant projection returns the new fields.
- Product summary projections were not changed because they do not include variants.

Backward compatibility:
- Existing variant creation without new fields defaults to unlimited capacity and null validity dates.
- Existing StockCount and IsAvailable behavior is unchanged.
- Existing ProductSession, cart, checkout, order, wallet, and API route paths were not changed.

Validation:
- Command: `dotnet restore .\Refahi.Backend.slnx`
  Result: Passed; existing NU1903 warnings for `System.Security.Cryptography.Xml` 9.0.0 in `Refahi.Modules.PaymentGateway.Infrastructure`.
- Command: `dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore`
  Result: Passed; existing NU1903 warnings, existing Store.Api nullability warnings in `ShopProductsEndpoints.cs`, and existing Refahi.Api version/nullability warnings.
- Command: `dotnet test .\Refahi.Backend.slnx --configuration Release --no-build`
  Result: Passed; Flights tests 11/11 and PaymentGateway tests 25/25.

Deferred items:
- UsageDate on cart/order.
- Runtime sold-count calculation.
- Capacity enforcement in checkout/payment.
- Orders query/projection for sold count.
- Concurrency/reservation/ledger.
- Performance optimization.
- Admin UI fields.
- Customer UI fields.
- Exclude dates.
- Holiday/calendar handling.
- Removal of ProductSession.
- Migration of legacy session data.

Risks / assumptions:
- The Store module currently uses PascalCase database column names, so the migration follows that existing convention instead of the prompt's recommended snake_case names.
- Existing ProductVariant domain changes and `VariantCapacityType` were present before this task and were treated as user/session changes.
- API enum serialization follows the current backend defaults; no new JSON enum converter was introduced.

Suggested next phase:
- Add frontend/admin field support and later implement UsageDate plus runtime sold-count/capacity enforcement through the Store cart/order flow without bypassing Orders or Wallet rules.
