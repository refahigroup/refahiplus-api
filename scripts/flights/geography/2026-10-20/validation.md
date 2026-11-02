# Validation — 2026-10-20

## Results

- Full backend solution build: passed, 0 errors. Existing deprecation/nullability
  and NuGet vulnerability warnings remain; dependency upgrades are outside this change.
- Flights C# suite on isolated PostgreSQL 17: 52 passed, 1 skipped (explicit
  provider sandbox credentials are not configured). The new batch References
  SQL reader and actual/commercial fallback behavior are exercised against PostgreSQL.
- Hotels C# suite: 112 passed, 1 skipped (its separate disposable hotel database
  variable is not configured).
- Flights geography Python tests: 11 passed.
- Existing Hotels geography Python tests: 24 passed.
- `verify_mapping.py`: passed on a new network-isolated PostgreSQL 17 container.
  It verifies full schema installation, release replay, YXU actual Canadian
  geography, original raw airports/memberships and Hotels mapping preservation,
  snapshot drift/mapping/new-ID conflict atomicity, FK restrictions, guarded
  rollback and reinstall. All temporary databases/containers are removed.
- Clean-checkout input reproduction: regenerated SQL SHA-256 matches
  the authoritative digest in `validation.json`.
- `git diff --check`: passed.

## Coverage and limits

The read-only local database snapshot contains 9,061 airports (including inactive
rows), 63 commercial cities, 105 selectable airports and 121 memberships.
5,689 airports and 57 commercial cities have evidence-backed mappings.
3,372 airports and 6 commercial cities remain explicitly queued. Of the 105
selectable airports, 70 have physical geography mappings; fallback keeps the
entire existing selection catalogue available.

The data package adds 5,314 canonical cities, 210 countries and 210 synthetic
foreign provinces. These are shared reference additions: existing consumers
that enumerate every active reference city will also see them after deployment.
Existing reference identities and Hotels mappings are unchanged. New names
without an approved Persian translation retain the public source caption.

Application data was read only for the geography export. Schema/data deployment
to that database and a live provider sandbox call were not performed. The release
must pass its snapshot guards on the intended deployment target before publishing
the API. Load/performance benchmarking of the expanded shared reference catalogue
has not been performed.
