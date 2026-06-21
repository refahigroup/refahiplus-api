Summary:
- Added domain-level validity range and capacity semantics to Store product variants.
- Kept ProductSession and cart SessionId model intact for later migration phases.

Skills used:
- architecture-guardian
- order-wallet-guardian

Files changed:
- src/Refahi.Modules.Store.Domain/Enums/VariantCapacityType.cs
- src/Refahi.Modules.Store.Domain/Entities/ProductVariant.cs
- src/Refahi.Modules.Store.Domain/Aggregates/Product.cs

Domain model changes:
- Added VariantCapacityType enum with Unlimited, TotalPeriod, and PerEligibleDay values.
- Added ProductVariant.FromDate, ToDate, CapacityType, and Capacity.
- Added ProductVariant.RequiresUsageDate for multi-day PerEligibleDay variants.
- Added ProductVariant.ValidateOrderEligibility(DateOnly? usageDate = null).
- Added ProductVariant.EnsureCapacityAvailable(int requestedQuantity, int soldCountInScope).

Business rules added:
- Validity range must provide both FromDate and ToDate, or neither.
- FromDate must be less than or equal to ToDate.
- Unlimited capacity normalizes Capacity to null.
- TotalPeriod and PerEligibleDay capacity require Capacity greater than zero.
- PriceMinor must be greater than zero.
- DiscountedPriceMinor, when present, must be greater than zero and lower than PriceMinor.
- Order eligibility rejects unavailable variants, missing required usage dates, and usage dates outside the validity range.
- Capacity checks reject invalid quantities, invalid sold counts, invalid configured capacity, and insufficient capacity.

Backward compatibility:
- Product.AddVariant keeps existing parameters and adds new validity/capacity parameters as optional trailing parameters.
- ProductVariant stock behavior and IsAvailable calculation remain based on StockCount.
- ProductSession, IProductSessionRepository, Product.Sessions, Product.AddSession, and CartItem.SessionId were not removed or modified.
- No application, API, infrastructure, migration, admin, Docker, package, or target framework changes were made.

Validation:
- Command: dotnet build .\src\Refahi.Modules.Store.Domain\Refahi.Modules.Store.Domain.csproj --configuration Release
  Result: Passed with 0 warnings and 0 errors.
- Command: dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore
  Result: Passed with 9 existing warnings and 0 errors. Warnings were outside the Store Domain change: PaymentGateway package NU1903 advisories, nullable warnings in Store.Api and Refahi.Api, and one Refahi.Api version-format warning.

Deferred TODOs:
- Sold count must be calculated by the application layer from successful non-cancelled/non-refunded order items.
- TODO: Enforce variant capacity atomically in checkout/payment to avoid oversell.
- TODO: Optimize runtime sold-count calculation via projection/cache/ledger before high-volume use.

Risks / assumptions:
- No database migration was added in this phase, so persistence mapping/schema updates are deferred.
- No application checkout/order flow was changed, so the new guards are available but not yet enforced by checkout.
- Calendar semantics such as even days, odd days, holidays, recurring weekdays, and excluded dates remain out of scope.

Suggested next phase:
- Add infrastructure persistence mapping and migration for the new ProductVariant fields, then update application/checkout flows to pass usage date, calculate soldCountInScope, and enforce capacity atomically.
