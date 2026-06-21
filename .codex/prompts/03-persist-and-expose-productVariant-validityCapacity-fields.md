# Codex Implementation Prompt — Phase 9: Persist and Expose ProductVariant Validity/Capacity Fields

Read `.codex/prompts/_session-bootstrap.md` first.
Then read `AGENTS.md`.
Load only the relevant skills from `.codex/skills/`.

This is a focused Backend Store persistence and contract implementation task.

Do not modify Admin UI.
Do not modify Customer WebApp.
Do not modify Cart/Checkout behavior.
Do not remove `ProductSession`.
Do not make release decisions.

## Mandatory Report Output

At the end of this task, save the final report as:

```text
.codex/reports/03-persist-and-expose-productVariant-validityCapacity-fields.md
```

Also print the same report in the Codex final response.

## Context

Phase 7 added domain-level validity and capacity semantics to `ProductVariant`.

Domain fields now exist:

```csharp
FromDate
ToDate
CapacityType
Capacity
RequiresUsageDate
ValidateOrderEligibility(DateOnly? usageDate = null)
EnsureCapacityAvailable(int requestedQuantity, int soldCountInScope)
```

Phase 8 planning confirmed that these fields are not yet persisted, migrated, projected, or exposed end-to-end through backend contracts.

This phase must make the existing domain fields available through Backend Store persistence and backend contracts only.

## Goal

Persist and expose `ProductVariant` validity/capacity fields end-to-end in the Backend Store module.

After this phase:

* EF must map the new `ProductVariant` fields.
* A Store migration must add the new columns.
* Backend DTOs must expose the new fields.
* Backend commands/validators must accept and validate the new fields when adding variants.
* Product detail/admin query projections must return the new fields.
* Existing legacy `ProductSession` flows must remain untouched.
* Existing product/session/cart/checkout behavior must not change.

## Repositories

Primary repository:

```text
C:\Workspace\repo\refahiplus-api
```

Admin repository should not be modified in this phase.

## Required Pre-Check

Run:

```bash
git status --short
```

in the Backend API repository.

If there are existing user changes, report them and do not touch unrelated files.

## Scope

Work in Backend API only.

Expected areas to inspect and possibly modify:

```text
src/Refahi.Modules.Store.Domain/Entities/ProductVariant.cs
src/Refahi.Modules.Store.Domain/Enums/VariantCapacityType.cs
src/Refahi.Modules.Store.Infrastructure/Persistence/Configurations/ProductVariantConfiguration.cs
src/Refahi.Modules.Store.Infrastructure/Persistence/Migrations/
src/Refahi.Modules.Store.Application.Contracts/
src/Refahi.Modules.Store.Application/
src/Refahi.Modules.Store.Api/
```

Likely contract/query/command areas:

```text
ProductVariantDto
ProductDetailDto
ProductSummaryDto if variants are projected there
Admin product detail DTOs
AddProductVariantCommand
AddProductVariantCommandValidator
Product detail query handlers
Admin product detail query handlers
Any variant projection helper/mapping code
```

Use actual file names from the repository. Do not invent paths if names differ.

## Required Backend Changes

### 1. EF Mapping

Update `ProductVariantConfiguration` to persist:

```csharp
FromDate
ToDate
CapacityType
Capacity
```

Expected mapping rules:

* `FromDate`: nullable date column.
* `ToDate`: nullable date column.
* `CapacityType`: required smallint/int column with default `Unlimited`.
* `Capacity`: nullable integer column.
* Preserve existing `StockCount`, `PriceMinor`, `DiscountedPriceMinor`, `IsAvailable`, and combination mappings.

Use the existing EF naming conventions in the Store module.

Recommended column names, unless existing conventions require otherwise:

```text
from_date
to_date
capacity_type
capacity
```

Add indexes only if consistent and low-risk.

Recommended safe indexes:

```text
product_id, capacity_type
product_id, from_date, to_date
```

Do not over-index.

### 2. EF Migration

Create a Store Infrastructure migration for the new columns.

Suggested migration name:

```text
AddProductVariantValidityCapacity
```

Do not apply the migration to any real database.

Migration must:

* Add nullable `from_date`.
* Add nullable `to_date`.
* Add non-null `capacity_type` with default `0` / `Unlimited`.
* Add nullable `capacity`.
* Update model snapshot.
* Preserve existing data.

Do not remove or alter `product_sessions`.

### 3. DTOs and Projections

Expose the new fields wherever product variants are returned.

At minimum, update variant DTOs used by:

* Admin product detail.
* Public/customer product detail.
* Product variant API responses.

Expose:

```csharp
DateOnly? FromDate
DateOnly? ToDate
VariantCapacityType CapacityType
int? Capacity
bool RequiresUsageDate
```

If DTO conventions avoid exposing enum types directly, follow existing project conventions. Do not introduce inconsistent serialization style.

Update query projections so the values come from `ProductVariant`.

Do not expose runtime sold count in this phase.

Do not expose remaining capacity in this phase unless it already exists and can be computed without order data. Prefer not to add it.

### 4. Add Variant Commands

Update variant creation contracts/commands to accept:

```csharp
DateOnly? FromDate
DateOnly? ToDate
VariantCapacityType CapacityType
int? Capacity
```

Ensure they flow into:

```csharp
Product.AddVariant(...)
```

Preserve backward compatibility:

* Existing callers that do not send these fields should behave as before.
* Default capacity type should be `Unlimited`.
* Capacity should be null for unlimited variants.

If there is an existing update-variant command/endpoint, update it as well.
If update-variant does not exist, do not create a broad new feature unless the existing architecture clearly expects it.

### 5. Validators

Update validators for add/update variant commands.

Validation rules must match Domain rules:

* If only one of `FromDate` or `ToDate` is provided, reject.
* If both are provided, `FromDate <= ToDate`.
* `CapacityType == Unlimited`: capacity must be null or ignored.
* `CapacityType == TotalPeriod`: capacity must be greater than zero.
* `CapacityType == PerEligibleDay`: capacity must be greater than zero.
* Keep existing price/discount/stock validation unchanged.
* Use Persian validation messages if existing validators use Persian messages.
* Preserve existing validator style and error conventions.

Do not validate calendar semantics such as even days, odd days, holidays, weekdays, or excluded dates.

### 6. API Endpoints

Ensure existing API endpoints that create variants receive and pass through the new fields.

Do not create new endpoints unless absolutely required by the existing architecture.

Do not modify session endpoints.

Do not modify cart/checkout endpoints in this phase.

### 7. Backward Compatibility

Preserve:

* Existing product creation.
* Existing variant creation without validity/capacity fields.
* Existing `StockCount` behavior.
* Existing `IsAvailable` behavior.
* Existing `ProductSession` behavior.
* Existing cart/checkout/order behavior.
* Existing API route paths.

### 8. Explicitly Deferred

Do not implement these in this phase:

* `UsageDate` on cart/order.
* Runtime sold-count calculation.
* Capacity enforcement in checkout/payment.
* Orders query/projection for sold count.
* Concurrency/reservation/ledger.
* Performance optimization.
* Admin UI fields.
* Customer UI fields.
* Exclude dates.
* Holiday/calendar handling.
* Removal of `ProductSession`.
* Migration of legacy session data.

## Validation

From Backend API repository root, run:

```bash
dotnet restore .\Refahi.Backend.slnx
dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore
dotnet test .\Refahi.Backend.slnx --configuration Release --no-build
```

If full test is too expensive or fails due unrelated known external reasons, still run the most focused Store-related build and clearly report what happened.

Do not claim validation passed unless commands actually pass.

## Final Report Format

Save the report to:

```text
.codex/reports/03-persist-and-expose-productVariant-validityCapacity-fields.md
```

Use exactly this structure:

```text
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

Persistence changes:
- ...

Migration:
- ...

Contract/DTO changes:
- ...

Command/validator changes:
- ...

Query/projection changes:
- ...

Backward compatibility:
- ...

Validation:
- Command: ...
  Result: ...

Deferred items:
- ...

Risks / assumptions:
- ...

Suggested next phase:
- ...
```
