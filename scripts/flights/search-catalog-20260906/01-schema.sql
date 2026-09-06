START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM flights."__EFMigrationsHistory" WHERE "MigrationId" = '20260906194439_FlightSearchCatalogue') THEN
    CREATE TABLE flights.catalog_imports (
        version varchar(50) PRIMARY KEY,
        applied_at_utc timestamptz NOT NULL,
        report jsonb NOT NULL
    );
    CREATE TABLE flights.catalog_backups (
        version varchar(50) NOT NULL,
        table_name varchar(50) NOT NULL,
        row_key text NOT NULL,
        row_data jsonb NULL,
        PRIMARY KEY (version, table_name, row_key)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM flights."__EFMigrationsHistory" WHERE "MigrationId" = '20260906194439_FlightSearchCatalogue') THEN
    CREATE TABLE flights.search_cities (
        city_code character varying(3) NOT NULL,
        city_name_fa character varying(200) NOT NULL,
        city_name_en character varying(200) NOT NULL,
        country_code character varying(2) NOT NULL,
        country_name_fa character varying(200) NOT NULL,
        country_name_en character varying(200) NOT NULL,
        CONSTRAINT "PK_search_cities" PRIMARY KEY (city_code)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM flights."__EFMigrationsHistory" WHERE "MigrationId" = '20260906194439_FlightSearchCatalogue') THEN
    CREATE TABLE flights.search_memberships (
        city_code character varying(3) NOT NULL,
        airport_code character varying(3) NOT NULL,
        is_domestic boolean NOT NULL,
        city_rank integer NOT NULL,
        airport_rank integer NOT NULL,
        CONSTRAINT "PK_search_memberships" PRIMARY KEY (is_domestic, city_code, airport_code),
        CONSTRAINT "FK_search_memberships_airports_airport_code" FOREIGN KEY (airport_code) REFERENCES flights.airports (iata_code) ON DELETE RESTRICT,
        CONSTRAINT "FK_search_memberships_search_cities_city_code" FOREIGN KEY (city_code) REFERENCES flights.search_cities (city_code) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM flights."__EFMigrationsHistory" WHERE "MigrationId" = '20260906194439_FlightSearchCatalogue') THEN
    CREATE INDEX "IX_search_memberships_airport_code" ON flights.search_memberships (airport_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM flights."__EFMigrationsHistory" WHERE "MigrationId" = '20260906194439_FlightSearchCatalogue') THEN
    CREATE INDEX "IX_search_memberships_city_code" ON flights.search_memberships (city_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM flights."__EFMigrationsHistory" WHERE "MigrationId" = '20260906194439_FlightSearchCatalogue') THEN
    CREATE UNIQUE INDEX "IX_search_memberships_is_domestic_airport_code" ON flights.search_memberships (is_domestic, airport_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM flights."__EFMigrationsHistory" WHERE "MigrationId" = '20260906194439_FlightSearchCatalogue') THEN
    INSERT INTO flights."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260906194439_FlightSearchCatalogue', '10.0.0');
    END IF;
END $EF$;
COMMIT;

