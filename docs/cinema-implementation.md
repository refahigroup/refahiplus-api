# Cinema and theater integration

## Architecture and deployment

Cinema owns reservation/issuance snapshots and provider attempts in the `cinema` schema. Orders owns payable orders; Wallets is accessed only by Orders. Customer payment precedes reseller confirmation. The reseller wallet at iTicket is a separate supplier account.

Backend API prefix: `/api/cinema`. Cinema uses exactly five existing projects. The new frontend module uses Domain/Application/Infrastructure/UI, shared Landing components and the existing Checkout. Rendering inventory is in the webapp checkout at `docs/page-rendering-inventory.md`.

The five required pre-existing architecture documents were not found: `01-refahi-overview.md`, `02-refahi-architecture.md`, `rendering-governance-constitution.md`, `page-rendering-inventory.md`, `rendering-architecture-migration-report.md`. No replacement overview or architecture text has been fabricated. Restore these originals and reconcile them before release.

## Configuration

All Cinema flags default false. Configure `Cinema:CatalogEnabled`, `Cinema:PurchaseEnabled`, and `Cinema:CancellationEnabled` separately. Disabling new purchases does not stop reconciliation. Existing issued orders remain readable.

Set `iTicket:BaseUrl=https://console.iticket.ir/api/v1/` and supply `iTicket:AccessToken` through environment variables or secret storage; never in a committed file. Set category ULIDs in `Cinema:iTicket:CinemaCategories`, `TheaterCategories`, and `ArtCategories`. Empty mappings intentionally return empty catalog sections. Configure banner placement and provider status vocabulary from the supplier contract. The supplied default vocabulary is reserved/confirmed/cancelled and must be verified on the actual reseller account.

`Cinema:SafetySeconds` defaults 60 (minimum 60). PayableUntil is provider expiry, capped by purchase-end, minus this margin. Missing provider expiry never produces a payable order. `WorkerSeconds` defaults 15 (minimum 5). All amounts are long IRR; IRT is multiplied by 10 with checked arithmetic. Unsupported currencies fail closed.

Apply generated migrations using the normal host migration mechanism, or generate a reviewed SQL script:

```sh
dotnet ef migrations script --project src/Refahi.Modules.Cinema.Infrastructure --startup-project src/Refahi.Modules.Cinema.Infrastructure --context CinemaDbContext --idempotent
```

PDF uses an embedded Estedad font and Microsoft.Playwright Chromium. Both Dockerfiles install the browser and native dependencies into `/ms-playwright`. Local setup after build:

```sh
node tests/Refahi.Modules.Cinema.Tests/bin/Debug/net10.0/.playwright/package/cli.js install chromium
```

## Operational recovery

The local reservation intent is durable before the external POST. Every mutating provider operation has an attempt recorded as Started/Completed/Failed/Ambiguous. Reads may retry; mutations never retry blindly.

- Lost reserve response without a provider ID: NeedsReview, no second reserve. Locate the booking through supplier support using the schedule/customer context. Do not refund or invent a provider ID based on guesswork.
- Known provider ID after malformed reserve response: retain that reference and reconcile/release it; do not create an Order from invalid totals.
- Lost confirm response: query provider order. If confirmed, finish local issuance; otherwise keep reviewing without another confirm POST.
- Ambiguous cancel: retain Pending cancellation and query provider. Refund through Orders only when cancellation is authoritative. Failed cancellation retains a valid ticket and is not retried by the worker.
- Cancellation succeeded but wallet refund failed: retry the Orders cancellation path, whose refund key preserves original allocations.
- Financial Order creation succeeded but the local link failed: recover using `cinema-order:{cinemaOrderId}`.

All payment/cancellation paths acquire the Orders lock before the Cinema lock. Worker paths release Cinema before requesting Orders cancellation. Never invert this order. Pending provider actions are excluded from a new payment. The source lease spans the wallet Reserve/Capture and financial state persistence.

Logs deliberately exclude access tokens, customer mobile/name and raw supplier payloads/errors. Alert on warning events for CinemaOrderId, stalled paid issuance, cancellation pending and reconciliation errors. Invalid/unknown status or currency must be resolved with the provider, not converted into a guessed success.

## Release gate

Verify account access to cinema/theater/art categories, seller balance, expiry/status vocabulary, actual customer total and supplier charges, maximum seats, full cancellation/refund semantics and accepted entrance codes for both cinema and theater. No barcode is generated without a confirmed supplier format. Live purchase/refund testing is still required before enabling flags.

## Verification

```sh
dotnet build Refahi.Backend.slnx
dotnet test tests/Refahi.Modules.Cinema.Tests/Refahi.Modules.Cinema.Tests.csproj
CINEMA_BROWSER_TESTS=1 dotnet test tests/Refahi.Modules.Cinema.Tests/Refahi.Modules.Cinema.Tests.csproj
# CINEMA_TEST_POSTGRES must identify a disposable, isolated test database.
CINEMA_TEST_POSTGRES='...' dotnet test tests/Refahi.Modules.Cinema.Tests/Refahi.Modules.Cinema.Tests.csproj
```

The seat-map fixture was captured from the public sample page on 2026-10-07. It contains catalog/schedule/seat information only, no personal data. Browser and PostgreSQL tests are explicitly opt-in; the latter migrates the supplied test database and must not target production.
