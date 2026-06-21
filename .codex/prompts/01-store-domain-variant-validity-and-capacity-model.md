# Codex Implementation Prompt — Phase 7: Store Domain Variant Validity and Capacity Model

Read `.codex/prompts/_session-bootstrap.md` first.
Then read `AGENTS.md`.
Load only the relevant skills from `.codex/skills/`.

This is a focused Backend Store Domain implementation task.

## Mandatory Report Output

At the end of this task, save the final report as:

```text
.codex/reports/01-store-domain-variant-validity-capacity.md
```

Also print the same report in the Codex final response.

## Context

Manual analysis found that the previous session-based product design is too rigid.

The business has two practical product shapes:

1. Products offered on one specific date.
2. Products offered across a validity period or recurring eligible days, such as:

   * روزهای زوج
   * روزهای فرد
   * روزهای تعطیل
   * هر دوشنبه

The new direction is:

* Keep `SalesModel.SessionBased` as a UI/business classification.
* Stop treating `SessionBased` as necessarily requiring `ProductSession`.
* Model the actual sellable options through `ProductVariant`.
* Add validity/capacity semantics to `ProductVariant`.
* Do not remove `ProductSession` yet in this phase because other application/API/Admin layers may still depend on it.

## Goal

Add a domain-level variant validity/capacity model to the Store Domain.

The goal is to support products whose sellable variant is valid in a date range and may have capacity rules.

Example:

```text
Product: استخر

Variant:
- مخاطب: آقایان
- روز: روزهای زوج
- قیمت: ۱۰۰۰ تومان
- FromDate: ۱۴۰۵/۰۵/۰۱
- ToDate: ۱۴۰۵/۰۵/۳۱
- CapacityType: TotalPeriod
- Capacity: ۳۰۰
```

Or:

```text
Variant:
- مخاطب: آقایان
- روز: روزهای زوج
- CapacityType: PerEligibleDay
- Capacity: ۳۰
```

## Scope

Work only in the Backend Store Domain project unless a compile issue inside the domain project requires a tiny adjustment.

Expected files:

```text
src/Refahi.Modules.Store.Domain/Entities/ProductVariant.cs
src/Refahi.Modules.Store.Domain/Aggregates/Product.cs
src/Refahi.Modules.Store.Domain/Enums/VariantCapacityType.cs
```

Also inspect:

```text
src/Refahi.Modules.Store.Domain/Entities/ProductSession.cs
src/Refahi.Modules.Store.Domain/Enums/SalesModel.cs
src/Refahi.Modules.Store.Domain/Exceptions/StoreDomainException.cs
src/Refahi.Modules.Store.Domain/Aggregates/Cart.cs
src/Refahi.Modules.Store.Domain/Entities/CartItem.cs
```

Do not modify application, API, infrastructure, Admin, Docker, GitHub Actions, package versions, or target frameworks in this phase.

## Required Domain Design

### 1. Add VariantCapacityType enum

Add a new enum under Store Domain enums:

```csharp
public enum VariantCapacityType : short
{
    Unlimited = 0,
    TotalPeriod = 1,
    PerEligibleDay = 2
}
```

Use XML summary comments in the existing style.

### 2. Extend ProductVariant

Add these properties to `ProductVariant`:

```csharp
public DateOnly? FromDate { get; private set; }
public DateOnly? ToDate { get; private set; }
public VariantCapacityType CapacityType { get; private set; }
public int? Capacity { get; private set; }
```

Meaning:

* `FromDate` / `ToDate`: optional validity range for the variant.
* `CapacityType = Unlimited`: no sale limit enforced by variant capacity.
* `CapacityType = TotalPeriod`: `Capacity` is the max sales count for the whole validity period.
* `CapacityType = PerEligibleDay`: `Capacity` is the max sales count per selected usage date.
* Runtime sold count must not be stored in the variant in this phase.

Do not add `SoldCount`.

### 3. Add domain rules

Add domain validation rules so:

* If only one of `FromDate` or `ToDate` is provided, reject it.
* If both are provided, `FromDate <= ToDate`.
* If `CapacityType == Unlimited`, `Capacity` must be null or ignored.
* If `CapacityType == TotalPeriod`, `Capacity` must be greater than zero.
* If `CapacityType == PerEligibleDay`, `Capacity` must be greater than zero.
* `PriceMinor` must remain greater than zero.
* `DiscountedPriceMinor`, if present, must remain valid according to current project conventions.
* Existing variant stock/price behavior should remain backward compatible.

### 4. Add helper behavior for usage-date requirement

Add a read-only property or method to `ProductVariant`:

```csharp
public bool RequiresUsageDate { get; }
```

Expected behavior:

```text
true only when:
- CapacityType == PerEligibleDay
- FromDate and ToDate both have values
- FromDate != ToDate
```

If `FromDate == ToDate`, usage-date selection is not required because the service is only available on one day.

### 5. Add order eligibility guard

Add a domain method to `ProductVariant` for structural order validation.

Suggested shape:

```csharp
public void ValidateOrderEligibility(DateOnly? usageDate = null)
```

The method should enforce only rules the domain can know without querying orders:

* Variant must be available.
* If a validity range exists and a usage date is provided, it must be within the range.
* If `RequiresUsageDate` is true, usage date must be provided.
* If `FromDate == ToDate` and usage date is null, treat it as acceptable.
* Do not check sold count here because sold count is runtime/order-data dependent.
* Do not implement concurrency here.

Use `StoreDomainException` with clear Persian messages and stable error codes.

### 6. Add capacity validation helper

Add a domain method that can be used by application/checkout later, without querying from the domain itself.

Suggested shape:

```csharp
public void EnsureCapacityAvailable(int requestedQuantity, int soldCountInScope)
```

Expected behavior:

* If quantity <= 0, throw.
* If `CapacityType == Unlimited`, return.
* If `Capacity` is required but missing or invalid, throw.
* If `soldCountInScope + requestedQuantity > Capacity`, throw insufficient capacity.
* For `TotalPeriod`, `soldCountInScope` means sales in the full period.
* For `PerEligibleDay`, `soldCountInScope` means sales for the requested usage date.

Add comments/TODOs explaining:

* Sold count must be calculated by application layer from successful non-cancelled/non-refunded order items.
* Concurrency must be handled in checkout/payment layer.
* Performance optimization must be handled through projection/cache/ledger later.

### 7. Update Product.AddVariant

Update `Product.AddVariant(...)` so callers can optionally pass the new variant validity/capacity fields.

Keep backward compatibility by using optional parameters with safe defaults.

Suggested default:

```text
CapacityType = Unlimited
Capacity = null
FromDate = null
ToDate = null
```

Do not break existing callers.

### 8. Preserve ProductSession for now

Do not delete these in this phase:

```text
ProductSession
IProductSessionRepository
Product.Sessions
Product.AddSession(...)
CartItem.SessionId
```

They will be handled in a later cross-layer migration plan.

You may add comments only if useful, but do not introduce `[Obsolete]` if it creates warnings or broad churn.

## Strict Out of Scope

Do not:

* Modify application commands/handlers.
* Modify API endpoints.
* Modify EF configurations/migrations.
* Modify repositories.
* Modify Admin Web.
* Modify cart/checkout/order flows.
* Remove ProductSession.
* Remove SessionId.
* Add database migrations.
* Implement concurrency.
* Implement performance projections.
* Implement holiday/calendar logic.
* Implement exclude dates.
* Implement manual QA.
* Change package versions or target frameworks.

## Important TODOs To Preserve In Code Comments

Add concise TODO comments only where useful:

```text
TODO: Enforce variant capacity atomically in checkout/payment to avoid oversell.
TODO: Optimize runtime sold-count calculation via projection/cache/ledger before high-volume use.
```

Do not over-comment.

## Validation

From Backend API repository root, run the most focused validation available.

Preferred:

```bash
dotnet build .\src\Refahi.Modules.Store.Domain\Refahi.Modules.Store.Domain.csproj --configuration Release
```

If project path differs, inspect and use the correct path.

If the full backend build is cheap and available, also run:

```bash
dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore
```

Do not claim validation passed unless commands actually pass.

## Final Report Format

Save the report to:

```text
.codex/reports/phase-7-store-domain-variant-validity-capacity.md
```

Use exactly this structure:

```text
Summary:
- ...

Skills used:
- ...

Files changed:
- ...

Domain model changes:
- ...

Business rules added:
- ...

Backward compatibility:
- ...

Validation:
- Command: ...
  Result: ...

Deferred TODOs:
- ...

Risks / assumptions:
- ...

Suggested next phase:
- ...
```
