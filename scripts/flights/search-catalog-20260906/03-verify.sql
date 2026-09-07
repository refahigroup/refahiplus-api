-- Read-only verification; expected: 40 domestic + 40 international cities, 121 memberships.
SELECT version, applied_at_utc, report FROM flights.catalog_imports WHERE version='search-catalog-20260906';
SELECT is_domestic, count(DISTINCT city_code) AS cities, count(*) AS airports
FROM flights.search_memberships GROUP BY is_domestic ORDER BY is_domestic DESC;
SELECT m.is_domestic,c.city_code,c.city_name_fa,c.country_name_fa,
       count(*) AS airport_count,string_agg(a.iata_code,',' ORDER BY a.iata_code) AS airport_codes
FROM flights.search_memberships m JOIN flights.search_cities c USING(city_code)
JOIN flights.airports a ON a.iata_code=m.airport_code
WHERE c.city_code IN ('THR','IST','DOH','LON')
GROUP BY m.is_domestic,c.city_code,c.city_name_fa,c.country_name_fa ORDER BY c.city_code,m.is_domestic;
-- Must return zero rows.
SELECT m.* FROM flights.search_memberships m LEFT JOIN flights.airports a ON a.iata_code=m.airport_code
WHERE a.iata_code IS NULL OR NOT a.is_active OR length(a.search_text)=0;
-- JSON precedence is deliberate: YXU now belongs to LON/GB in this catalogue.
SELECT iata_code,city_code,country_code FROM flights.airports WHERE iata_code='YXU';
-- Actual before/after identity changes on this database (the source-audit.md compares the bundled reference).
SELECT a.iata_code,b.row_data->>'city_code' AS previous_city,a.city_code AS current_city,
 b.row_data->>'country_code' AS previous_country,a.country_code AS current_country,
 b.row_data->>'airport_name_en' AS previous_name,a.airport_name_en AS current_name
FROM flights.catalog_backups b JOIN flights.airports a ON a.iata_code=b.row_key
WHERE b.version='search-catalog-20260906' AND b.table_name='airports' AND b.row_data IS NOT NULL
 AND (b.row_data->>'city_code' IS DISTINCT FROM a.city_code OR b.row_data->>'country_code' IS DISTINCT FROM a.country_code
 OR b.row_data->>'airport_name_en' IS DISTINCT FROM a.airport_name_en)
ORDER BY a.iata_code;
