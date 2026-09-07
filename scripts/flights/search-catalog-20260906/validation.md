# Validation — Flights search catalogue

Executed locally on 2026-09-06. No production database or real booking/payment endpoint was used.

## Automated results

| Check | Result |
|---|---|
| Backend Flights tests | 19 passed, 1 skipped (sandbox CITY smoke test) |
| Frontend Flights tests | 9 passed |
| Full frontend solution, Debug | Passed; 2 existing missing-Commerce-project warnings |
| Full backend solution, Release | Passed; existing solution/package warnings |
| Full backend solution, Debug | Blocked by existing solution configuration: References.Infrastructure has Build=false for Debug while References.Api requires it |
| References.Api standalone Debug build | Passed, without source/configuration changes |
| EF pending model changes | None |
| Generated data SQL reproducibility | Identical SHA-256 after regeneration |
| git diff whitespace validation | Passed in both repositories |

The backend Debug failure is at `src/Refahi.Modules.References.Api/DI.cs:7`; its cause is
the existing `<Build Solution="Debug|*" Project="false" />` entry under References.Infrastructure
in `Refahi.Backend.slnx`. This unrelated solution setting was not changed.

## PostgreSQL integration

Tests used an isolated PostgreSQL 17 Alpine container with random per-test databases:

- Applied earlier EF migrations, then the delivered `01-schema.sql` twice; EF reported no pending migrations.
- Executed the delivered data SQL, repeated it, then ran the actual embedded-SQL Seeder.
- Verified byte-equivalent JSON snapshots of imported airport rows across repeated executions.
- Ran the delivered rollback SQL and verified restoration of original rows and absence of new memberships.
- Re-imported through the Seeder after rollback.
- Verified preservation of existing ICAO/coordinates, Persian fallback and unrelated active airport rows.
- Verified JSON precedence for YXU (LON/GB), 121 memberships and 40 selectable cities per mode.
- Queried the actual EF repository for Tehran, Istanbul, Doha, Persian/Latin names and member IATA codes.
- Verified grouping/counts before text matching/limit and rejection of malformed or unlisted locations.
- Exercised the search handler with a recording provider: domestic inference, international inference,
  foreign-to-foreign routes, rejection before provider calls and Airport/City reversal on return legs.
- Existing booking lifecycle and Order preparation regression tests passed.

The provider recorder does not establish live SnappTrip CITY support.

## Frontend and visual checks

bUnit rendered the actual form, picker and SSR result components. Tests cover mode remapping,
clearing unavailable selections, preserving dates/passengers/types in the search URL, retry,
stale-response suppression, two-line captions, selected option accessibility state, SSR hidden
fields, HTTP query contracts, Persian validation and cancellation propagation.

Static HTML exported from those rendered components was inspected in the in-app browser with
the actual default-theme, shared UI and Flights CSS at 375×812 and 1280×900. The mobile form
and international picker and the desktop form were visually checked; measured document widths
matched viewport widths (375/375 and 1280/1280), with no horizontal page overflow.
This visual check used fixture data and does not claim an installed-PWA or live-WASM end-to-end run.

The published service worker was inspected: same-origin `/api/` uses network-only and navigation
uses network-first. There was no service-worker code change.

## Remaining environment-dependent acceptance

- Live SnappTrip CITY acceptance: not run because an explicitly identified sandbox URL/key was not available.
  `FlightCitySandboxTests` is included and skips with a reason until `FLIGHT_SANDBOX_BASE_URL` and
  `FLIGHT_SANDBOX_API_KEY` are provided. Existing production credentials are not reused automatically.
- After deployment, perform a hard refresh with the installed service worker, verify actual WASM network
  traffic and exercise search → offer → passengers → existing Checkout in the intended test environment.
- Production SQL execution is deliberately left to the operator, following `README.md`.
