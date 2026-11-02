# Tochal sparse geography schema upgrade — 2026-10-27

Target: `tochal-db` in `infra_postgres`, role `refahi`, API container `tochal_api`. These names and the two Tehran records were confirmed by the user. The original reference geography snapshot contains hundreds of different identities and must not be installed here.

## Scope

The SQL preserves both Tehran IDs at 1 and leaves all existing province/city fields, sequences, foreign keys and logical business references untouched. It adds the V4 runtime schema: Iran country identity, geography columns/constraints/indexes, real audit tables and provider-owned Hotel mapping tables. Audit tables are empty because no merge/reorganization occurs. Provider mapping tables are empty because there is no verified provider mapping snapshot for this tenant. No provider IDs, mappings, booking capability or full reference dataset are fabricated.

The existing Flights and Hotels migrations remain unchanged and retain their guards. The API applies its actual pending EF migrations after restart; this package does not edit EF history or create Flight mapping tables in their place. This is the V4 **schema contract** for a sparse tenant, not installation of the full Refahi V4 dataset. Hotel/Flight searches need separate verified provider mappings before they can return offers.

## Run

Upload the ZIP to `/root/tochal-geography-schema.zip`, then:

```bash
unzip /root/tochal-geography-schema.zip -d /root/tochal-geography-schema
cd /root/tochal-geography-schema
bash apply-tochal-geography-schema.sh
```

The script verifies the package hashes, stops only the crash-looping Tochal API, creates a private custom-format full database backup under `/root/tochal-geography-backups`, and checks the archive table of contents. This is not a restore drill. The SQL runs in one transaction and rejects a different database or province/city snapshot. A SQL failure rolls back its changes and leaves the API stopped for investigation. No automatic database restore is attempted. A previously prepared and tested backup/restore process should remain available.

After SQL COMMIT, the script starts only `tochal_api`, prints recent logs, and checks public health and modules endpoints. Repeated 502 after the health wait means recovery is incomplete; send the new logs. Check the homepage category cards too. The script preserves the container's current settings, including the TouristPanel override from the previous recovery step; it does not recreate other services or change nginx.

After successful recovery, keep the SQL, backup path/hash and console output. Do not run the earlier generic `2026-10-19-v4-complete.sql` against this tenant. This script can be replayed while the confirmed sparse snapshot remains unchanged; it intentionally rejects subsequent province/city changes.

## Validation

Run `python scripts/hotels/geography/test_tochal_schema.py --docker <docker-executable>` from the backend repository. Tests use a disposable PostgreSQL 17 container without network access or published ports. They cover preservation of both records, declared and logical business references, sequence high-water mark, replay, wrong-database rejection, snapshot/country conflicts, rollback after a late DDL failure, actual checked-in Flight migration SQL, Hotel guard/backfill SQL, the runtime References geography query and mapping FK enforcement. They do not claim a full production startup test or a database restore drill.
