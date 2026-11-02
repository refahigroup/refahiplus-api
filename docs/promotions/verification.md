# Promotions implementation verification — 2026-10-17

- Backend Release solution: 417 passed, 7 existing environment-dependent tests skipped, 0 failed; includes 32 Promotions tests with no skips.
- Customer Webapp Release solution tests: 126 passed, 6 existing browser tests skipped, 0 failed.
- Admin Release build: successful, 0 compiler errors.
- Customer Webapp Release build: checked separately from the test projects.
- EF pending-model checks: no pending changes in Promotions, Organizations, Orders or Wallets.
- git diff --check: clean in all three repositories.

PostgreSQL 17 ran in a dedicated local Docker container with separate Promotions and Wallet test databases. A fresh Promotions database exercised all migrations, including the existing Orders ownership backfill's dependency on the real Store schema. Tests cover quotas/budgets under concurrency, membership revocation, exact two-source financial lifecycle, destination/in-person postings, response privacy, durable outbox recovery, held-payment continuation, HTTP access/versioning/rate limits and code encryption/export.

The seven skipped backend cases belong to existing Flight database/sandbox and Cinema database/PDF fixtures whose dedicated environment configuration was not supplied. The six skipped Webapp cases are existing Cinema browser fixtures. No live provider purchase, production deployment, production wallet funding or authenticated manual browser acceptance was performed. Existing dependency-advisory/nullable/analyzer warnings remain; unrelated package upgrades were not included.

Default: promotions disabled until operators configure a valid, funded IRR System wallet and enable Promotions__Enabled. See architecture-and-operations.md for configuration, recovery and deployment order. Preserve the shared Data Protection key ring.

Local command output is retained under .artifacts: backend-tests-final.log, promotions-tests-final.log, web-tests-final.log, admin-build-final.log, web-build-final.log and the four *-model-check.log files. These are local build artifacts, not source-controlled deployment input.
