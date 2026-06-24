# Codex Operational QA Prompt — Phase 25B: Controlled ShopProductVariant Backfill Execution and Fixture QA

Read `.codex/prompts/_session-bootstrap.md` first.
Then read `AGENTS.md`.
Load only the relevant backend/module/store/order architecture skills from `.codex/skills/`.

This is an **operational QA and controlled data-readiness task**.

Run this prompt from:

```text id="n4u9xw"
C:\Workspace\repo\refahiplus-api
```

Do not modify source code unless a tiny report-only/doc-only change is required.
Do not modify Admin Web.
Do not modify Customer WebApp.
Do not modify database migrations.
Do not modify Docker/GitHub Actions.
Do not remove runtime fallback behavior.
Do not run write-mode backfill on production.
Do not make release decisions.

## Mandatory Report Output

At the end of this task, save the final report as:

```text id="w5zzql"
.codex/reports/phase-25b-shopproductvariant-backfill-execution-qa.md
```

If `.codex/reports/` does not exist, create it.

Also print the same report in the Codex final response.

## Context

Phase 25A added backend-only controlled audit and backfill tooling for `ShopProductVariant`.

Available admin endpoints:

```text id="s5gktw"
GET  /admin/shop-product-variants/backfill/audit
POST /admin/shop-product-variants/backfill
```

Phase 25A behavior:

```text id="fw9p8l"
- DryRun defaults to true.
- Backfill writes only missing non-deleted ShopProductVariant offerings.
- Existing configured offerings are never overwritten.
- Inactive non-deleted offerings are treated as existing and are not overwritten.
- Soft-deleted offerings do not block creating a new non-deleted offering.
- Backfill uses ProductVariant.PriceMinor and ProductVariant.DiscountedPriceMinor.
- Runtime fallback to ProductVariant pricing remains active.
- No actual database backfill was executed in Phase 25A.
```

Current goal:

```text id="e7711l"
Use the Phase 25A tooling in a controlled environment to verify audit, dry-run, write-mode backfill, idempotency, and end-to-end pricing behavior for products with variants.
```

## Critical Safety Rules

### Production Safety

Do not run write-mode backfill against production.

Write-mode is allowed only if all of these are true:

```text id="8xds4r"
1. The environment is explicitly local, development, or stage.
2. The API base URL clearly indicates local/dev/stage.
3. A valid admin token is available.
4. A narrow ShopId or ProductId scope is used for the first write run.
5. The prompt/session explicitly has PHASE25B_ALLOW_WRITE=true or equivalent explicit local/stage approval.
```

If any condition is missing:

```text id="m3gnv4"
- Run audit and dry-run only.
- Do not run write-mode.
- Report what was skipped and why.
```

### Data Safety

Do not:

```text id="sfh6zx"
- overwrite existing ShopProductVariant rows;
- change ProductVariant prices;
- change ShopProduct prices;
- change stock/capacity;
- change orders/cart data;
- remove fallback;
- delete data;
- apply migrations to a real database.
```

## Required Pre-Check

Run:

```bash id="cr5tnf"
git status --short
```

Report dirty state clearly.

Do not modify source files unless necessary for the report.

## Required Environment Discovery

Check whether local/stage API execution is possible.

Look for existing environment/run instructions:

```text id="ke4j5h"
README
docs
.env examples
docker-compose files
launchSettings.json
appsettings.Development.json
AGENTS.md
.codex reports
```

Find or infer:

```text id="fvt51n"
API_BASE_URL
admin auth/token mechanism
Store module route prefix
whether local API is running
whether database is reachable
```

Do not expose secrets in the report.

If an admin token is present only as an environment variable, reference it as:

```text id="dhs03d"
ADMIN_TOKEN: present/not present
```

Do not print the token.

## Required Operational Flow

### 1. Build/Test Baseline

Run backend validation first:

```bash id="uopvpi"
dotnet restore .\Refahi.Backend.slnx
dotnet build .\Refahi.Backend.slnx --configuration Release --no-restore
dotnet test .\Refahi.Backend.slnx --configuration Release --no-build
```

Do not proceed to API write-mode if build/test fails.

### 2. Audit All or Scoped Data

If API and admin auth are available, call audit endpoint.

Preferred first call:

```http id="juhsth"
GET /admin/shop-product-variants/backfill/audit
```

If dataset may be large, use filters:

```http id="0enbfx"
GET /admin/shop-product-variants/backfill/audit?shopId={shopId}
GET /admin/shop-product-variants/backfill/audit?productId={productId}
GET /admin/shop-product-variants/backfill/audit?shopId={shopId}&productId={productId}
```

Capture:

```text id="mhhygg"
ShopProductsChecked
ProductsWithVariants
ExistingOfferings
MissingOfferings
first-N detail rows
warnings/errors
```

If API/auth is not available, report audit as not run and explain exactly what is missing.

### 3. Dry-Run Backfill

Run dry-run before any write.

Example payload:

```json id="k59jgj"
{
  "shopId": null,
  "productId": null,
  "dryRun": true,
  "detailLimit": 50
}
```

Prefer scoped dry-run first if sample ShopId/ProductId is available.

Capture:

```text id="695w3d"
CreatedOfferings preview count
SkippedExistingOfferings
SkippedInvalidVariants
Warnings
CreatedItems preview
```

Do not run write-mode if dry-run returns invalid price warnings that look unsafe.

### 4. Controlled Write-Mode Backfill

Only if safety conditions are met.

Start with a narrow scope:

```json id="nphh4y"
{
  "shopId": "{sampleShopId}",
  "productId": "{sampleProductId}",
  "dryRun": false,
  "detailLimit": 50
}
```

After write-mode, immediately run:

```text id="cml1m6"
- audit for the same scope;
- write-mode again for the same scope to confirm idempotency.
```

Expected result:

```text id="9pp6xc"
- First write creates missing rows.
- Second write creates zero rows.
- Existing configured rows are not overwritten.
- Audit missing count decreases.
```

If write-mode is not allowed/possible, document the exact API calls that should be run manually later.

### 5. Two-Shop Fixture QA

If data exists or can be safely identified:

Find or use a product that exists in two shops and has variants.

Verify:

```text id="7kegq9"
- Shop A has ShopProductVariant row for Variant X.
- Shop B has ShopProductVariant row for Variant X.
- Prices can differ between Shop A and Shop B.
- Product detail with shopSlug A returns price A.
- Product detail with shopSlug B returns price B.
```

Use API calls if browser QA is not available:

```http id="6o1502"
GET /api/store/{moduleSlug}/products/{productSlug}?shopSlug={shopSlugA}
GET /api/store/{moduleSlug}/products/{productSlug}?shopSlug={shopSlugB}
```

Check returned variant fields:

```text id="6iwld3"
ShopProductVariantId
ShopPriceMinor
ShopDiscountedPriceMinor
PriceSource
IsActiveInShop
UsesShopSpecificPrice
PriceMinor / DiscountedPriceMinor as currently returned
```

### 6. Cart/Checkout API QA

If local/stage API, auth, and test data are available:

Test at least one variant item with active `ShopProductVariant`.

Verify:

```text id="z2t6zt"
- AddToCart uses ShopProductVariant price.
- GetCart returns ShopProductVariantId and PriceSource.
- PlaceStoreOrder succeeds when cart price is current.
- If price changes after item is in cart, checkout rejects stale price or cart shows HasPriceChanged according to Phase 21 behavior.
```

Do not use real payment rails unless local/stage mocks are explicitly configured.

Do not run wallet/payment flows against production.

### 7. WebApp Browser QA

If local/stage WebApp and API are available:

Verify:

```text id="ze98lz"
Same product under Shop A and Shop B:
- shop-specific route passes ShopSlug;
- variant selector shows Shop A price on Shop A route;
- variant selector shows Shop B price on Shop B route;
- inactive shop variant is disabled;
- missing shop variant fallback still works if expected;
- cart row displays correct price;
- price-change warning blocks checkout when backend says HasPriceChanged.
```

If browser QA is unavailable, report not run.

## Optional: Manual Command Templates

If executing API calls is not possible, provide exact curl/httpie templates using placeholders.

Do not invent real IDs.

Template examples:

```bash id="lc4i39"
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  "$API_BASE_URL/admin/shop-product-variants/backfill/audit?shopId={SHOP_ID}&productId={PRODUCT_ID}"
```

```bash id="j8m6gw"
curl -X POST \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"shopId":"{SHOP_ID}","productId":"{PRODUCT_ID}","dryRun":true,"detailLimit":50}' \
  "$API_BASE_URL/admin/shop-product-variants/backfill"
```

```bash id="i39dgl"
curl -X POST \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"shopId":"{SHOP_ID}","productId":"{PRODUCT_ID}","dryRun":false,"detailLimit":50}' \
  "$API_BASE_URL/admin/shop-product-variants/backfill"
```

## Strict Out of Scope

Do not:

```text id="t17dvu"
- modify Backend source behavior;
- modify Admin Web;
- modify Customer WebApp;
- remove ProductVariant fallback;
- enforce missing ShopProductVariant rejection;
- apply DB migrations;
- run write-mode on production;
- run unscoped write-mode as the first write;
- change cart/checkout code;
- change product detail code;
- change Orders/Wallet;
- change Docker/GitHub Actions;
- change package versions or target frameworks;
- mark production-ready.
```

## Final Report Format

Save the report to:

```text id="f40rd1"
.codex/reports/phase-25b-shopproductvariant-backfill-execution-qa.md
```

Use exactly this structure:

```text id="jv3u6s"
Summary:
- ...

Skills used:
- ...

Repository checked:
- ...

Git state:
- Pre-check:
- Final:

Environment/runtime availability:
- API base URL:
- Admin token:
- Environment classification:
- Write-mode allowed:
- Reason:

Build/test validation:
- Command: ...
  Result: ...

Audit execution:
- Endpoint:
- Scope:
- Result:
- Missing offerings:
- Warnings:

Dry-run execution:
- Endpoint:
- Scope:
- Result:
- Would create:
- Skipped existing:
- Skipped invalid:
- Warnings:

Write-mode execution:
- Executed: yes/no
- Scope:
- Result:
- Created:
- Idempotency re-run:
- Reason if not executed:

Two-shop product-detail QA:
- Executed: yes/no
- Result:
- Evidence:

Cart/checkout QA:
- Executed: yes/no
- Result:
- Evidence:

WebApp/browser QA:
- Executed: yes/no
- Result:
- Evidence:

Data safety findings:
- ...

Fallback behavior:
- ...

Deferred items:
- ...

Risks / assumptions:
- ...

Suggested next phase:
- ...
```
