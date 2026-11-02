# Flights geography — 2026-10-20

For the supplied production snapshot, use the separate
[`2026-10-20-production` package](2026-10-20-production/README.md).
It has its own release version, input, timezone-safe timestamp guard, Verify and
Rollback scripts. The original local release below remains unchanged.

This release connects **all 9,061 rows** of the local `flights.airports` snapshot
to canonical References geography where the pinned evidence identifies a city.
It preserves the 63 commercial cities / 105 selectable airports / 121 memberships.
No application database has been modified by building or verifying this package.

## Ownership and runtime

- `flights.airport_geography_mappings`: airport IATA → actual reference city.
- `flights.snapptrip_city_mappings`: commercial city code → reference city.
- Country and province are derived from References, never owned by Flights.
- References reads use `GetCityGeographiesQuery` through MediatR, in batches.
  Its Infrastructure reader selects the V4 country/province/city columns without
  adding those SQL-owned tables/columns to the EF migration model.
- Airport responses and flight segment captions use actual geography. Location
  responses and search origin/destination captions use commercial geography.
- IATA, `City/Airport`, route validation, grouping, counts, ranks, legacy URLs and
  provider request codes remain unchanged. Historical offer/booking snapshots
  are not rewritten.
- Missing/inactive mappings retain raw captions and nullable reference fields;
  reference infrastructure failures propagate as infrastructure errors.
- Text search includes canonical names before limiting results. Airport text
  queries batch-read mapped reference identities; they do not query each airport.

Additive response fields:

| Response | Reference fields |
|---|---|
| airports | `geographicReferenceCityId`, `geographicReferenceProvinceId`, `geographicReferenceCountryCode` |
| locations | `commercialReferenceCityId`, `commercialReferenceProvinceId`, `commercialReferenceCountryCode` |
| search | origin/destination `CommercialReferenceCityId`, `CommercialReferenceProvinceId`, `CommercialReferenceCountryCode` |
| offer segments | departure/arrival `GeographicReferenceCityId`, `GeographicReferenceProvinceId`, `GeographicReferenceCountryCode` |

## Reviewed rules and evidence

The inputs contain only geography. `2026-10-20/inputs/geography.json.gz` is a
consistent, read-only local database export (including inactive airports).
`evidence.json.gz` contains relevant public OurAirports rows, candidate GeoNames
populated places, countries, source URLs and original SHA-256 hashes. GeoNames
is CC BY 4.0; OurAirports is public domain. No credentials, users or transactions
are included. Source archives and temporary execution logs belong in `.local/`.

An actual-airport mapping requires a unique non-closed OurAirports IATA identity,
no contradictory ICAO, corroborating snapshot coordinates (within 20 km when
available), and an **exact municipality name/alias in the same country** matching
a unique GeoNames populated place within 80 km. Distance alone never maps a city.
Conflicting/reused airport codes are queued. Commercial cities use exact names
and country independently, never an airport's physical location. When available,
bilingual candidate sets must agree; a unique exact primary name disambiguates
broad alternate-name aliases, while conflicting identities stay queued.

Reference matches are scoped by the real Iranian admin1/province, or a foreign
country's single synthetic province. Multiple identities, ambiguous names and
inactive references are queued, not merged. Existing reference identities and
Hotels mappings are preserved. New countries and cities use names from the
evidence; where no approved Persian caption exists, their documented source name
is retained rather than claiming a verified Persian translation.

`validation.json` contains measured counts and hashes. `remaining.json` accounts
for every unresolved airport/commercial city. `conflicts.json` records resolved
raw/actual differences; other identity conflicts stay in `remaining.json`.
The compressed report includes additions and per-mapping evidence. Mapping
coverage is not a claim of currently bookable commercial inventory.

## Deployment

1. Require existing Flights migrations through
   `20260907120000_FlightCustomerPayablePricing` and **References geography V4**.
2. Back up the database and schedule a maintenance window. The import takes locks.
3. Run `2026-10-20/01-schema.sql` with `psql -X -v ON_ERROR_STOP=1`.
4. Run `2026-10-20/02-data.sql` in its own session/transaction with the same flags.
5. Run `03-verify.sql`, retain the output, then deploy the new API.

The data SQL embeds its complete input and needs only a PostgreSQL SQL client.
All SQL files also accept direct execution in Navicat: use UTF-8 and disable
Continue on error. They contain no psql-only backslash commands; when using psql,
keep `-X -v ON_ERROR_STOP=1` on the command line.

The data SQL checks
the exported airport/city/membership identities and original References rows,
refuses new-ID or existing-mapping conflicts, and records a content digest in
`flights.geography_releases`. A target with different data must have a newly
reviewed export/release; do not weaken the drift guards. The migration's References
FKs are explicit SQL because cross-module EF entities are forbidden.

Replaying the identical schema/data release is safe. Startup applies schema
migrations but **does not import/research geography**. The existing catalogue
seeder keeps its original versioned SQL; geography does not mutate its inputs.

For rollback, stop the new API, deploy the previous API and run
`04-rollback-data.sql`. It verifies each release mapping before removing it and
refuses to delete subsequently changed mappings. Raw Airports and membership
rows remain untouched. Shared reference additions are retained deliberately:
later logical consumers may not have FKs. The receipt lists those additions for
separate reviewed cleanup. EF schema downgrade is guarded; leave additive tables.

## Reproduction and verification

From the backend repository root, Python 3.11+ / standard library:

```powershell
python -X utf8 scripts/flights/geography/build_mapping.py --output scripts/flights/geography/.local/rebuilt
python -X utf8 -m unittest discover -s scripts/flights/geography -p 'test_*.py'
python -X utf8 scripts/flights/geography/verify_mapping.py
```

The verifier requires Docker with `postgres:17-alpine` and the .NET EF tools.
It generates full migration SQL when absent, creates a temporary container with
no network/published ports, and tests install/replay, drift/conflict atomicity,
FKs, rollback/reinstall and preservation of raw rows and Hotels mappings. It
never connects to the application database.

The C# tests use an explicitly supplied isolated server via
`FLIGHT_CATALOG_TEST_CONNECTION` and `FLIGHT_FARE_TEST_CONNECTION`; they create and
drop random databases. `FlightCitySandboxTests` separately requires explicit
provider sandbox settings and performs no payment or reservation.

To produce a new snapshot, select the intended container/database explicitly:

```powershell
python -X utf8 scripts/flights/geography/snapshot.py --docker-container CONTAINER --database DATABASE --output scripts/flights/geography/.local/geography.json.gz
```

`prepare_evidence.py --help` documents refreshing the evidence from archived
OurAirports CSV, GeoNames `cities500.zip` and `countryInfo.txt`. New evidence is
a new release; keep published inputs/SQL immutable. No frontend routes or WASM
components change in this release; existing rendering inventory remains valid.
