# Tochal API startup recovery — 2026-10-27

The homepage and static CSS return 200, but `/api/health` and `/api/store/modules` return nginx 502. `tochal_api` restarts with exit 139 and OOMKilled=false. Its logs identify an unhandled `InvalidOperationException` from `TouristPanelOptions.Validate`, not evidence of a native runtime fault. The shipped base and Development settings enabled TouristPanel and sales with empty connection credentials, making the optional provider prevent the entire API from starting.

The repository now defaults both flags to false. Enabled providers still require valid connection and sales settings. Environments using TouristPanel must explicitly enable it and supply their own complete credentials/contract settings. In particular, review the healthy Refahi production environment before deploying these new defaults there: credentials alone no longer enable the provider or its sales.

## Restore Tochal without waiting for a new image

In the existing Tochal Compose API service's `environment` mapping, set:

```yaml
Commerce__Providers__TouristPanel__Enabled: "false"
Commerce__Providers__TouristPanel__SalesEnabled: "false"
```

Preserve all other settings. This disables TouristPanel integration and sales; it does not change Store data or redirect Tochal to another environment's API. If Tochal requires TouristPanel, complete its connection and sales configuration before re-enabling it. Do not bypass validation or copy another tenant's secrets.

From the directory and with the same Compose files/project options used for the existing deployment, identify and recreate only the API service:

```bash
docker inspect tochal_api --format '{{index .Config.Labels "com.docker.compose.service"}}'
docker compose config --quiet
docker compose up -d --no-deps --force-recreate <service-name-from-inspect>
docker logs --since 2m --tail 100 tochal_api
docker inspect tochal_api --format 'status={{.State.Status}} exit={{.State.ExitCode}} restarts={{.RestartCount}}'
curl --fail-with-body https://tochalvip.ir/api/health
curl --fail-with-body https://tochalvip.ir/api/store/modules
```

Use the real Compose service name, which may differ from `tochal_api`. A simple `docker restart` does not apply changed environment variables. If configuration is JSON mounted into the container instead, update these same two values in that source and recreate the API service. An `.env` entry only works when the Compose service actually references it.

Require a stable running container, successful JSON health/modules responses and visible category links on a fresh homepage request. If another startup exception appears, diagnose that exception rather than claiming recovery. The current nginx upstream `tochal_api:8080` matches the supplied container name; these findings do not require an nginx upstream change.

## Local validation

The regression tests load the actual shipped JSON settings and verify that provider registration succeeds without credentials and does not register TouristPanel. An explicitly enabled provider with missing credentials must still fail. No provider requests or financial transactions are made by these tests.

## Follow-up: Reference geography V4 prerequisite

After disabling TouristPanel, the supplied server logs show startup proceeding to `20261020152038_FlightGeographyMappings`. Its guard fails because `"references".province_reorganization_history` is absent. The API therefore still returns 502. The application image is ahead of this database's geography deployment; this is a separate blocker from TouristPanel.

Do not create an empty marker table, remove the guard, mark the migration as applied, or copy Refahi's database over Tochal. The reviewed V4 installer is `scripts/hotels/geography/2026-10-19-v4/2026-10-19-v4-complete.sql`; its README documents data movement, snapshot guards and transaction behavior. It supports the original reference dataset and V1/V2/V3 upgrades, but conflicting live data must be reviewed rather than bypassed.

First confirm the database name actually used by `tochal_api` (do not assume `refahidb` from the other environment). Copy `deployment/tochal-geography-preflight.sql` to the server and run it against that database with a role that can read these tables:

```bash
docker cp /absolute/server/path/tochal-geography-preflight.sql infra_postgres:/tmp/tochal-geography-preflight.sql
docker exec infra_postgres psql -X -U <database-role> -d <confirmed-tochal-database> -v ON_ERROR_STOP=1 -f /tmp/tochal-geography-preflight.sql
```

The preflight is read-only and prints relation existence, counts and module migration history, not credentials or personal records. Review this inventory before choosing an installer. Before the data-changing V4 installation, take and verify a backup of this exact database and use a maintenance window as required by the V4 README. Do not proceed past a snapshot conflict; obtain a current sanitized geography snapshot and regenerate/review the migration if needed. The production Flight data package is snapshot-specific and must not be applied to Tochal merely because its name contains “production.”

Once the reviewed V4 installer commits successfully, restart only the Tochal API and examine the complete new startup logs. Require successful `/api/health`, `/api/store/modules`, and actual category rendering before declaring recovery. Any additional missing prerequisite must be handled explicitly.

### Confirmed Tochal inventory

The user confirmed the database name `tochal-db`. The successful read-only preflight ran as `refahi` and found exactly one province and one city, no countries or geography history/mapping tables, and Flights migrations applied through `20260907120000_FlightCustomerPayablePricing`. This is a sparse tenant dataset, not the original complete References snapshot expected by the generic V4 installer. Do not run the included `2026-10-19-v4-complete.sql` on this database. Obtain the current province/city rows and their column definitions before preparing and testing a tenant-compatible upgrade that preserves their existing IDs and references. The previously delivered ZIP is a transfer package, not approval that its generic installer matches this database.

The follow-up rows confirm Tehran province and city both have ID 1, English name `Tehran`, slug `tehran`, sort order 1 and active=true. City ProvinceId is 1. The targeted schema upgrade is now `deployment/tochal-geography-schema.sql`; follow `deployment/tochal-geography-schema-README.md` and its backup-first runner. It preserves these identities, installs the full required schema contract without importing the other tenant's geography dataset, and leaves provider mappings empty. Six PostgreSQL 17 integration tests pass, including replay, business-reference preservation, migration SQL compatibility and rollback on late failure. This local validation does not establish that production has recovered; require the actual startup/API/homepage checks.

### Runtime-role permissions follow-up

The user's next console output confirms the schema transaction committed, Tehran IDs stayed 1, and the API passed the Flights migration, but HotelsReferenceCitySearch still failed its information_schema guard. The original integration tests used the administrator and missed role visibility: PostgreSQL information_schema.columns omits administrator-created tables when the API role lacks privileges. This exact failure is now reproduced with an unprivileged runtime role.

Run `deployment/tochal-geography-permissions.sql` as `refahi` against `tochal-db` after stopping the crash-looping API. It derives the API role from the matching owners of the two Flight mapping tables that the API just created, rejects missing/ambiguous ownership, grants only schema USAGE and SELECT on the nine explicit geography/audit/mapping tables, and checks the Hotel guard under that role before COMMIT. It changes no business records, owners, role attributes, EF history or provider mappings. The schema-only release requires this follow-up when installer and API use different roles; do not reinstall the generic V4 dataset.

Eight PostgreSQL 17 tests now pass, including the failing restricted-role guard before repair, successful guard after repair, idempotent grants, ambiguous-role rejection and denial of writes/superuser privileges. Production permission diagnosis is confirmed only after this script's role notice and successful role check; if it still fails, retain its error and investigate rather than broadening grants. Start the API after successful COMMIT and verify health, modules and the homepage.
