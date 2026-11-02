BEGIN;
SELECT pg_advisory_xact_lock(174593,20261019);
DO $rollback$
DECLARE payload jsonb; row jsonb; old jsonb; section text; table_name text; key_name text;
BEGIN
 SELECT receipt INTO payload FROM flights.geography_releases WHERE version='flight-geography-20261020-production' FOR UPDATE;
 IF payload IS NULL THEN RETURN; END IF;
 FOREACH section IN ARRAY ARRAY['airports','commercial'] LOOP
   table_name=CASE WHEN section='airports' THEN 'airport_geography_mappings' ELSE 'snapptrip_city_mappings' END;
   key_name=CASE WHEN section='airports' THEN 'AirportIataCode' ELSE 'ProviderCityCode' END;
   EXECUTE format('LOCK TABLE flights.%I IN SHARE ROW EXCLUSIVE MODE',table_name);
   FOR row IN SELECT value FROM jsonb_array_elements(payload->section) LOOP
     EXECUTE format('SELECT to_jsonb(m) FROM flights.%I m WHERE %I=$1',table_name,key_name) INTO old USING row->>'code';
     IF old IS NULL OR old->>'CityId'<>row->>'city_id' OR old->>'SourceVersion'<>row->>'source_version'
        OR old->'Evidence'<>row->'evidence' OR old->>'SourceIdentity'<>row->>'source_identity'
        OR old->>'MatchMethod'<>row->>'method' OR old->>'SourceSnapshotDate'<>row->>'snapshot_date'
     THEN RAISE EXCEPTION 'Mapping changed since release; rollback stopped'; END IF;
     EXECUTE format('DELETE FROM flights.%I WHERE %I=$1',table_name,key_name) USING row->>'code';
   END LOOP;
 END LOOP;
 -- Shared reference additions are intentionally retained: later logical consumers may have no FK.
 DELETE FROM flights.geography_releases WHERE version=payload->>'version';
END $rollback$;
COMMIT;
