# Hotel provider geography data releases

The production data release is `2026-10-19-v4/2026-10-19-v4-complete.sql`.
It is self-contained: production execution needs PostgreSQL/psql, not Python,
provider credentials, raw API snapshots or files from a developer workstation.
Read `2026-10-19-v4/README.md` before deployment. The SQL changes data/schema;
runtime consumption of country/provider mappings is separate work.

## Versioned files

- Released V1–V4 SQL and templates: retained for audit and upgrade regression tests.
- Mapping reports, research resolutions, official evidence and explicit user decisions:
  inputs/provenance needed to reproduce the reviewed V4 release.
- `2026-10-19-live-inventory/inventory-audit.json`: curated read-only API audit;
  V4 uses its reviewed city/hotel identities. This is not the full provider inventory.
- `fixtures/reference-geography.sql`: **test/generator input only**, containing
  exactly the 31 province and 1,319 city rows from the original geography baseline.
  It intentionally retains four duplicate city rows to exercise the migration.
  It contains no users, addresses, orders, wallets, credentials or transaction data.
- Generators, validators, tests and the provider-table naming audit.

Raw API responses, source ZIPs, database backups, credentials and execution logs
must stay outside Git. Use `.local/` for local sources and outputs. The redundant
live inventory `cities.csv`, `hotels.csv` and run validation are ignored; regenerate
them from the API audit when needed. Ignoring a file does not delete its local copy.

Historical reports retain source paths and SHA-256 values as provenance, including
paths on the original researcher's workstation. Those paths are **not required**
by V4 generation or regression tests. Never reinterpret a historical path as an
instruction to retrieve a production database backup.

## Reproduce V4 from a checkout

Requires Python 3.11+ (standard library only). Run from the repository root:

```powershell
python scripts/hotels/geography/build_mapping_v4.py --output scripts/hotels/geography/.local/rebuilt-v4
python -m unittest discover -s scripts/hotels/geography -p 'test_*.py'
python scripts/hotels/geography/verify_mapping_v4.py
```

V4 defaults to the bundled reference-only fixture, checked-in V3 report/SQL,
reviewed API audit and user decisions. `--dump PATH` can select another geography
input, and `--output PATH` keeps regenerated output away from published files.
The two regenerated SQL files must have the same SHA-256 values as
`2026-10-19-v4/validation.json`. Reports may have different source paths/hashes
because the bundled fixture replaces the private, full database dump.
The folder's `.gitattributes` preserves published SQL/JSON/CSV bytes, and the V4
generator explicitly retains the published SQL's CRLF on Windows and Linux.
Do not normalize line endings in released evidence or migration files.

The PostgreSQL integration verifier needs Docker with `postgres:17-alpine`
available. It creates a temporary container without network or published ports,
tests fresh and V1/V2/V3 upgrades plus replay/conflict rollback, then removes it.
It never connects to the application database.

Older release verification also accepts the bundled fixture:

```powershell
python scripts/hotels/geography/verify_mapping_v2.py --dump scripts/hotels/geography/fixtures/reference-geography.sql --version 3
```

## Optional: repeat the original research or live API audit

These operations require separately archived inputs; they are not needed to run
the release or V4 tests. Use the original SHA-256 values in V2/V3 reports and
`resolutions.json` to identify the correct snapshots. New GeoNames downloads or
new provider inventory are new evidence, not byte-for-byte historical inputs.

For V2/V3 generation, supply `--dump`, `--snapshot` (the three SnappTrip/Lamasoo
raw city/inventory JSON files), and `--gazetteer` (GeoNames `cities500.zip`,
`IQ.zip`, `admin1CodesASCII.txt`, `countryInfo.txt`). GeoNames publishes data at
https://download.geonames.org/export/dump/ under CC BY 4.0. Provider endpoints
are `https://api.lamasoo.com/booking/cities`,
`https://api.lamasoo.com/booking/hotel_inventory`, and
`https://b2bapiv2.snapptrip.com/cities/`; authentication must come from local
configuration. Do not store authentication headers in snapshots/manifests.

For the research engine, supply explicit paths:

```powershell
python scripts/hotels/geography/research_resolutions.py --dump scripts/hotels/geography/fixtures/reference-geography.sql --sources PATH_TO_RESEARCH_SOURCES --gazetteer PATH_TO_GEONAMES --inventory PATH_TO_ARCHIVED_LAMASOO_INVENTORY --output scripts/hotels/geography/.local/research
```

Research sources include the pinned Iranian cities/provinces/counties JSON
identified by URLs and hashes in `resolutions.json`, OurAirports `airports.csv`
and `regions.csv`, and full GeoNames IR/TR/AE/ID/MY/LK ZIPs. OurAirports publishes
CSV data at https://davidmegginson.github.io/ourairports-data/.

`check_lamasoo_inventory.py --snapshot PATH --output PATH` additionally requires
an authenticated initial Cities/Inventory snapshot and its hash manifest. It
reads credentials from the supplied local `--config` file (default application
configuration), performs only GET requests, and stores no credentials in output.
Use `.local/` for fresh snapshots; do not overwrite the curated release evidence.
