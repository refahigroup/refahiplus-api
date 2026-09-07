# Flight fare source code storage

The flight search failure `22001: value too long for type character varying(1000)`
comes from the provider fare source token stored in
`flights.flight_search_offer_snapshots.provider_fare_source_code`.
Booking copies that same token to `flights.flight_offer_snapshots.provider_fare_id`,
which previously had a smaller 200-character limit.

Migration `20260906203006_FlightFareSourceCodesAsText` changes both columns to
PostgreSQL `text`, preserving existing values and complete provider tokens.
Tokens must not be truncated or hashed because booking sends them back to the provider.
The model snapshot includes the preceding `FlightSearchCatalogue` migration.

Deploy the updated API and restart it. Flight module startup calls
`DbTools.ApplyMigrations<FlightsDbContext>()` against the configured application
database. Verify that startup reports migration success before retrying search.
For a separately managed database deployment, apply the Flights EF migrations
using the environment's configured connection. The design-time factory has its
own local default connection; do not assume it targets the running API database.

Downgrade refuses to narrow either column while longer tokens exist, preventing
silent token corruption. Resolve retained data before attempting a downgrade.
No frontend route or render mode changes are involved.

Regression test: set `FLIGHT_FARE_TEST_CONNECTION` to an isolated PostgreSQL test
server with database creation permission, then run:

```powershell
dotnet test tests/Refahi.Modules.Flights.Tests/Refahi.Modules.Flights.Tests.csproj --filter FlightFarePersistenceTests
```

The test creates and removes its own temporary database, reproduces SQLSTATE
22001 before migration, preserves an existing offer, and round-trips an
8,000-character token through search snapshot and booking persistence. It also
checks that downgrade cannot truncate the stored token. No provider booking or
payment is invoked.
