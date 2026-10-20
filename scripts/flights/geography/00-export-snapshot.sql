-- Geography-only export. Run with psql -X -Atq -v ON_ERROR_STOP=1 and redirect stdout to a JSON file.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SELECT jsonb_build_object(
 'airports',(SELECT jsonb_agg(to_jsonb(a) ORDER BY iata_code) FROM flights.airports a),
 'search_cities',(SELECT jsonb_agg(to_jsonb(c) ORDER BY city_code) FROM flights.search_cities c),
 'search_memberships',(SELECT jsonb_agg(to_jsonb(m) ORDER BY is_domestic,city_code,airport_code) FROM flights.search_memberships m),
 'cities',(SELECT jsonb_agg(to_jsonb(c) ORDER BY "Id") FROM "references".cities c),
 'provinces',(SELECT jsonb_agg(to_jsonb(p) ORDER BY "Id") FROM "references".provinces p),
 'countries',(SELECT jsonb_agg(to_jsonb(c) ORDER BY "Code") FROM "references".countries c),
 'v4_installed',to_regclass('"references".province_reorganization_history') IS NOT NULL);
COMMIT;
