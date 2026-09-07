-- Stop the new API first: its Seeder intentionally reapplies the catalogue if the version is absent.
-- Keep the schema until data rollback has completed. No bookings/orders/payment rows are changed.
BEGIN;
SELECT pg_advisory_xact_lock(620260906);
DO $rollback$
BEGIN
 IF NOT EXISTS (SELECT 1 FROM flights.catalog_imports WHERE version='search-catalog-20260906') THEN
   RAISE NOTICE 'Catalogue is not applied'; RETURN;
 END IF;
 IF EXISTS (SELECT 1 FROM flights.catalog_imports WHERE version <> 'search-catalog-20260906') THEN
   RAISE EXCEPTION 'Roll back later catalogue imports first';
 END IF;
 DELETE FROM flights.search_memberships;
 DELETE FROM flights.search_cities;
 -- Airport references are restricted by PostgreSQL; rollback aborts atomically if new references prevent removal.
 DELETE FROM flights.airports WHERE iata_code IN (
   SELECT row_key FROM flights.catalog_backups WHERE version='search-catalog-20260906' AND table_name='airports');
 INSERT INTO flights.airports SELECT (jsonb_populate_record(NULL::flights.airports,b.row_data)).*
 FROM flights.catalog_backups b WHERE version='search-catalog-20260906' AND table_name='airports' AND row_data IS NOT NULL;
 INSERT INTO flights.search_cities SELECT (jsonb_populate_record(NULL::flights.search_cities,b.row_data)).*
 FROM flights.catalog_backups b WHERE version='search-catalog-20260906' AND table_name='search_cities';
 INSERT INTO flights.search_memberships SELECT (jsonb_populate_record(NULL::flights.search_memberships,b.row_data)).*
 FROM flights.catalog_backups b WHERE version='search-catalog-20260906' AND table_name='search_memberships';
 DELETE FROM flights.catalog_imports WHERE version='search-catalog-20260906';
 DELETE FROM flights.catalog_backups WHERE version='search-catalog-20260906';
END;
$rollback$;
COMMIT;
