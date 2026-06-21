# Codex Planning Prompt — Phase 8: Cross-Layer Migration Plan for Variant-Based Session Products

Read `.codex/prompts/_session-bootstrap.md` first.
Then read `AGENTS.md`.
Load only the relevant skills from `.codex/skills/`.

This is a **planning-only task**.

Do not modify source files.
Do not create migrations.
Do not implement code.
Only create the required planning report under `.codex/reports/`.

## Mandatory Report Output

At the end of this task, save the final report as:

```text
.codex/reports/02-cross-layer-migration-plan-for-variant-based-session-products.md
```

Also print the same report in the Codex final response.

## Context

The Store Domain is moving toward variant-based session/access products.

Final business direction:

* Keep `SalesModel.SessionBased` for UI/business classification.
* Do not model all session-based products with `ProductSession`.
* Use `ProductVariant` as the sellable option for session/access products.
* `ProductVariant` may have:

  * `FromDate`
  * `ToDate`
  * `CapacityType`
  * `Capacity`
* `VariantCapacityType` values:

  * `Unlimited`
  * `TotalPeriod`
  * `PerEligibleDay`
* Do not store `SoldCount` on `ProductVariant`.
* Runtime sold count must be calculated from successful order/payment data.
* If `CapacityType == PerEligibleDay` and `FromDate != ToDate`, the user must choose a usage date at order time.
* If `FromDate == ToDate`, choosing a date is unnecessary because only one day is possible.
* Holiday/calendar eligibility is not enforced by the system in the first version.
* Provider/admin warnings and product notices are used to communicate usage limitations.
* Exclude dates may be added later if needed.
* Concurrency and performance are explicit TODOs and must be planned carefully.

## Goal

Inspect the Backend API, Admin Web, cart/checkout/order flows, and persistence layer, then produce a cross-layer migration plan.

The plan should explain how to move from `ProductSession`-driven session products to variant-based session/access products without breaking existing flows.

## Scope

Inspect both repositories:

```text
C:\Workspace\repo\refahiplus-api
C:\Workspace\repo\refahiplus-admin
```

Expected backend areas:

* Store Domain
* Store Application commands/queries/DTOs
* Store API endpoints
* Store Infrastructure EF configurations/migrations/repositories
* Cart flows
* Checkout/order/payment integration
* Any handlers that currently require `SessionId`
* SupplyChain agreement product model where `SalesModel` is defined or consumed

Expected admin areas:

* Product create/edit page
* Product variant UI
* Product session editor UI
* AdminApiClient product/session/variant DTOs and methods
* Warnings/notices product fields if present

## Planning Questions

Answer these questions:

### 1. Current Session Dependency Map

Find all current references to:

```text
ProductSession
IProductSessionRepository
SessionId
CreateProductSession
UpdateProductSession
CancelSession
SalesModel.SessionBased
```

Group them by:

* Domain
* Application
* API
* Infrastructure
* Admin Web
* Cart/checkout/order
* DTOs/contracts

### 2. Variant Validity/Capacity Integration Points

Identify where these new ProductVariant fields must be added:

```text
FromDate
ToDate
CapacityType
Capacity
RequiresUsageDate
```

Group changes by:

* Domain
* Application contracts
* API requests/responses
* EF configuration/migration
* Admin API client
* Admin UI
* Customer/public product detail
* Cart
* Checkout/order

### 3. Cart and Order Design

Plan how cart/order should represent usage date.

Questions to answer:

* Should `CartItem` get `UsageDate`?
* Should `OrderItem` get `UsageDate`?
* When is usage date required?
* What happens when `FromDate == ToDate`?
* How should duplicate cart items merge?

  * Same product/variant/usage date?
  * Same product/variant without usage date?
* How does checkout validate capacity?
* How does checkout compute sold count?
* Which order/payment states count as sold?
* How are cancelled/refunded orders excluded?

### 4. Capacity Enforcement Plan

Plan capacity enforcement for:

* `Unlimited`
* `TotalPeriod`
* `PerEligibleDay`

Explicitly document:

* Where runtime sold count is computed.
* Which repository/query should provide it.
* Where atomic concurrency control must happen.
* What is acceptable for first implementation.
* What must be marked as TODO for high-volume safety.

### 5. Performance Plan

Identify performance risks and propose first-step mitigation.

Topics:

* Avoid sold-count queries in product listing.
* Product detail can include lightweight availability hints if cheap.
* Checkout must perform authoritative capacity check.
* Future projection/cache/ledger option.

### 6. Admin UI Plan

Plan Admin changes:

* Remove or hide current `ProductSessionEditor` for the new model.
* Add variant fields:

  * FromDate
  * ToDate
  * CapacityType
  * Capacity
* Keep `SessionBased` as UI classification if needed.
* Use product warnings/notices to communicate usage restrictions.
* Explain how providers define examples like:

  * آقایان + روزهای زوج
  * خانم‌ها + روزهای فرد
  * تعطیل
* Explain how `PerEligibleDay` should warn admins that customers must choose usage date.

### 7. Customer UI Plan

Plan customer/product detail changes:

* Show validity period.
* Show variant choices.
* Ask for usage date only when required.
* Do not ask usage date when `FromDate == ToDate`.
* Display warnings/notices clearly.
* Show capacity/availability only if reliable.

### 8. Legacy ProductSession Strategy

Decide what to do with existing `ProductSession` flows:

Options:

* Keep temporarily but hide for new products.
* Migrate existing session-based products to variant-based model.
* Remove after all references are replaced.

Recommend the safest phased strategy.

### 9. Implementation Phases

Propose implementation phases after this plan.

At minimum include:

* Phase A: Persistence/contracts for ProductVariant validity/capacity
* Phase B: Admin variant UI
* Phase C: Customer product detail/cart usage date
* Phase D: Checkout/order capacity enforcement
* Phase E: Remove/deprecate old ProductSession flows
* Phase F: Performance/concurrency hardening

For each phase include:

* Goal
* Files likely to change
* Validation commands
* Risks

## Strict Rules

* Do not modify files.
* Do not implement code.
* Do not create migrations.
* Do not delete ProductSession in planning.
* Do not invent unsupported backend behavior.
* Do not mark production-ready.
* Preserve all current architecture constraints.
* Keep module boundaries clean.

## Final Report Format

Save the report to:

```text
.codex/reports/02-cross-layer-migration-plan-for-variant-based-session-products.md
```

Use exactly this structure:

```text
Summary:
- ...

Skills used:
- ...

Repositories inspected:
- ...

Current session dependency map:
- ...

Variant validity/capacity integration points:
- ...

Cart/order usage date plan:
- ...

Capacity enforcement plan:
- ...

Performance/concurrency plan:
- ...

Admin UI plan:
- ...

Customer UI plan:
- ...

Legacy ProductSession strategy:
- ...

Recommended implementation phases:
- ...

Risks / assumptions:
- ...

Next implementation prompt:
- ...
```
