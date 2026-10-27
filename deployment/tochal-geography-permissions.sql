-- Grant the API's migration-owning role read access to administrator-created geography.
-- Derive that role from the two Flight tables created by the API in this recovery.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '15s';
DO $permissions$
DECLARE api_role name; other_role name;
BEGIN
    IF current_database() <> 'tochal-db' THEN
        RAISE EXCEPTION 'This release is only for tochal-db';
    END IF;
    SELECT pg_get_userbyid(relowner) INTO api_role
        FROM pg_class WHERE oid=to_regclass('flights.airport_geography_mappings');
    SELECT pg_get_userbyid(relowner) INTO other_role
        FROM pg_class WHERE oid=to_regclass('flights.snapptrip_city_mappings');
    IF api_role IS NULL OR other_role IS DISTINCT FROM api_role THEN
        RAISE EXCEPTION 'Cannot identify one API migration role from the two Flight mapping tables';
    END IF;
    IF NOT EXISTS (SELECT FROM information_schema.columns WHERE table_schema='hotels'
        AND table_name='lamaso_city_mappings' AND column_name='IsEnabled') THEN
        RAISE EXCEPTION 'Administrator cannot see the completed geography schema; inspect the target database';
    END IF;
    RAISE NOTICE 'Granting geography read access to API migration role: %', api_role;
    EXECUTE format('GRANT USAGE ON SCHEMA "references", hotels TO %I', api_role);
    EXECUTE format('GRANT SELECT ON TABLE
        "references".countries, "references".cities, "references".provinces,
        "references".city_merge_history, "references".province_reorganization_history,
        hotels.snapptrip_city_mappings, hotels.lamaso_city_mappings,
        hotels.snapptrip_province_mappings, hotels.lamaso_province_mappings TO %I', api_role);

    -- Check visibility as the actual API role, rather than the administrator.
    EXECUTE format('SET LOCAL ROLE %I', api_role);
    IF to_regclass('hotels.snapptrip_city_mappings') IS NULL
       OR to_regclass('hotels.lamaso_city_mappings') IS NULL
       OR NOT EXISTS (SELECT FROM information_schema.columns WHERE table_schema='hotels'
           AND table_name='lamaso_city_mappings' AND column_name='IsEnabled') THEN
        RAISE EXCEPTION 'Hotel geography remains invisible to the API role';
    END IF;
    PERFORM "Code" FROM "references".countries WHERE "Code"='IR';
    PERFORM "ProviderCityId", "CityId", "IsEnabled" FROM hotels.lamaso_city_mappings LIMIT 1;
    RESET ROLE;
END $permissions$;
COMMIT;
