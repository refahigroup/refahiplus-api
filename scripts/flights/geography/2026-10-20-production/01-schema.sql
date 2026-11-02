START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM flights."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152038_FlightGeographyMappings') THEN
    DO $guard$ BEGIN
        IF to_regclass('"references".province_reorganization_history') IS NULL
        THEN RAISE EXCEPTION 'Reference geography V4 must be installed before FlightGeographyMappings'; END IF;
    END $guard$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM flights."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152038_FlightGeographyMappings') THEN
    CREATE TABLE flights.airport_geography_mappings (
        "AirportIataCode" varchar(3) PRIMARY KEY REFERENCES flights.airports("iata_code") ON DELETE RESTRICT,
        "CityId" integer NOT NULL REFERENCES "references".cities("Id") ON DELETE RESTRICT,
        "SourceIdentity" text NOT NULL, "MatchMethod" text NOT NULL, "Evidence" jsonb NOT NULL,
        "SourceVersion" text NOT NULL, "SourceSnapshotDate" date NOT NULL, "CreatedAtUtc" timestamptz NOT NULL);
    CREATE INDEX "IX_airport_geography_mappings_CityId" ON flights.airport_geography_mappings("CityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM flights."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152038_FlightGeographyMappings') THEN
    CREATE TABLE flights.snapptrip_city_mappings (
        "ProviderCityCode" varchar(3) PRIMARY KEY REFERENCES flights.search_cities("city_code") ON DELETE RESTRICT,
        "CityId" integer NOT NULL REFERENCES "references".cities("Id") ON DELETE RESTRICT,
        "SourceIdentity" text NOT NULL, "MatchMethod" text NOT NULL, "Evidence" jsonb NOT NULL,
        "SourceVersion" text NOT NULL, "SourceSnapshotDate" date NOT NULL, "CreatedAtUtc" timestamptz NOT NULL);
    CREATE INDEX "IX_snapptrip_city_mappings_CityId" ON flights.snapptrip_city_mappings("CityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM flights."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152038_FlightGeographyMappings') THEN
    INSERT INTO flights."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020152038_FlightGeographyMappings', '10.0.0');
    END IF;
END $EF$;
COMMIT;

