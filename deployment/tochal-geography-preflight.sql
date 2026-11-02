-- Read-only inventory before installing Reference geography V4 on Tochal.
\set ON_ERROR_STOP on
BEGIN READ ONLY;

SELECT current_database() AS database_name, current_user AS database_role;

SELECT name AS relation_name, to_regclass(name) IS NOT NULL AS installed
FROM (VALUES
    ('"references".countries'),
    ('"references".provinces'),
    ('"references".cities'),
    ('"references".city_merge_history'),
    ('"references".province_reorganization_history'),
    ('hotels.snapptrip_city_mappings'),
    ('hotels.lamaso_city_mappings'),
    ('flights.airport_geography_mappings'),
    ('flights.snapptrip_city_mappings')
) AS expected(name);

SELECT format('SELECT %L AS relation_name, count(*) AS row_count FROM %I.%I;',
              schema_name || '.' || table_name, schema_name, table_name)
FROM (VALUES
    ('references', 'countries'), ('references', 'provinces'), ('references', 'cities'),
    ('references', 'city_merge_history'), ('references', 'province_reorganization_history'),
    ('hotels', 'snapptrip_city_mappings'), ('hotels', 'lamaso_city_mappings')
) AS expected(schema_name, table_name)
WHERE to_regclass(format('%I.%I', schema_name, table_name)) IS NOT NULL
\gexec

SELECT format('SELECT %L AS module, "MigrationId" FROM %I."__EFMigrationsHistory" ORDER BY "MigrationId";',
              schema_name, schema_name)
FROM (VALUES ('references'), ('flights'), ('hotels')) AS modules(schema_name)
WHERE to_regclass(format('%I."__EFMigrationsHistory"', schema_name)) IS NOT NULL
\gexec

COMMIT;
