DO $import$
DECLARE
 payload jsonb := __PAYLOAD__::jsonb;
 row jsonb; old jsonb; table_name text; key_name text; section text; existing_hash text;
BEGIN
 IF to_regclass('"references".province_reorganization_history') IS NULL
    OR NOT EXISTS (SELECT 1 FROM information_schema.columns col WHERE col.table_schema='references'
        AND col.table_name='provinces' AND col.column_name='IsSynthetic')
 THEN RAISE EXCEPTION 'Reference geography V4 is required'; END IF;
 LOCK TABLE "references".countries,"references".provinces,"references".cities IN SHARE ROW EXCLUSIVE MODE;
 LOCK TABLE flights.airports,flights.search_cities,flights.search_memberships IN SHARE MODE;
 LOCK TABLE flights.airport_geography_mappings,flights.snapptrip_city_mappings IN SHARE ROW EXCLUSIVE MODE;
 CREATE TABLE IF NOT EXISTS flights.geography_releases(
   version text PRIMARY KEY, digest text NOT NULL, receipt jsonb NOT NULL, applied_at timestamptz NOT NULL DEFAULT now());
 SELECT digest INTO existing_hash FROM flights.geography_releases WHERE version=payload->>'version';
 IF existing_hash IS NOT NULL AND existing_hash<>payload->>'digest'
 THEN RAISE EXCEPTION 'Release version/hash conflict'; END IF;

 -- A release may only be applied to the exact exported source, including inactive rows.
 IF (SELECT count(*) FROM flights.airports)<>jsonb_array_length(payload->'expected_airports')
    OR EXISTS (SELECT 1 FROM jsonb_array_elements(payload->'expected_airports') r
      LEFT JOIN flights.airports a ON a.iata_code=r->>'iata_code' WHERE (to_jsonb(a)-'imported_at_utc') IS DISTINCT FROM (r-'imported_at_utc')
        OR a.imported_at_utc IS DISTINCT FROM (r->>'imported_at_utc')::timestamptz)
 THEN RAISE EXCEPTION 'Airport snapshot drift; regenerate the release'; END IF;
 IF (SELECT count(*) FROM flights.search_cities)<>jsonb_array_length(payload->'expected_commercial')
    OR EXISTS (SELECT 1 FROM jsonb_array_elements(payload->'expected_commercial') r
      LEFT JOIN flights.search_cities c ON c.city_code=r->>'city_code' WHERE to_jsonb(c) IS DISTINCT FROM r)
 THEN RAISE EXCEPTION 'Commercial snapshot drift'; END IF;
 IF (SELECT count(*) FROM flights.search_memberships)<>jsonb_array_length(payload->'expected_memberships')
    OR EXISTS (SELECT 1 FROM jsonb_array_elements(payload->'expected_memberships') r
      LEFT JOIN flights.search_memberships m ON m.city_code=r->>'city_code'
        AND m.airport_code=r->>'airport_code' AND m.is_domestic=(r->>'is_domestic')::boolean
      WHERE to_jsonb(m) IS DISTINCT FROM r)
 THEN RAISE EXCEPTION 'Membership snapshot drift'; END IF;
 FOREACH section IN ARRAY ARRAY['countries','provinces','cities'] LOOP
   key_name=CASE WHEN section='countries' THEN 'Code' ELSE 'Id' END;
   FOR row IN SELECT value FROM jsonb_array_elements(payload->('expected_'||section)) LOOP
     EXECUTE format('SELECT to_jsonb(t) FROM "references".%I t WHERE %I=$1%s',section,key_name,
       CASE WHEN section='countries' THEN '' ELSE '::integer' END)
       INTO old USING row->>key_name;
     IF old IS NULL OR NOT old @> row THEN RAISE EXCEPTION 'Reference snapshot drift in %',section; END IF;
   END LOOP;
   FOR row IN SELECT value FROM jsonb_array_elements(payload->'additions'->section) LOOP
     EXECUTE format('SELECT to_jsonb(t) FROM "references".%I t WHERE %I=$1%s',section,key_name,
       CASE WHEN section='countries' THEN '' ELSE '::integer' END)
       INTO old USING row->>key_name;
     IF old IS NOT NULL AND NOT old @> row THEN RAISE EXCEPTION 'New reference identity conflict in %',section; END IF;
     IF old IS NULL THEN
       IF section='countries' THEN
         INSERT INTO "references".countries("Code","Name","NameEn","IsActive")
           VALUES(row->>'Code',row->>'Name',row->>'NameEn',true);
       ELSIF section='provinces' THEN
         INSERT INTO "references".provinces("Id","Name","NameEn","Slug","SortOrder","IsActive","CountryCode","GeoNamesAdmin1Code","IsSynthetic")
           VALUES((row->>'Id')::int,row->>'Name',row->>'NameEn',row->>'Slug',(row->>'SortOrder')::int,true,row->>'CountryCode',NULL,true);
       ELSE
         INSERT INTO "references".cities("Id","Name","NameEn","Slug","ProvinceId","SortOrder","IsActive","LocationKind")
           VALUES((row->>'Id')::int,row->>'Name',row->>'NameEn',row->>'Slug',(row->>'ProvinceId')::int,(row->>'SortOrder')::int,true,'city');
       END IF;
     END IF;
   END LOOP;
 END LOOP;
 IF EXISTS (SELECT 1 FROM "references".provinces WHERE "CountryCode"<>'IR'
     GROUP BY "CountryCode" HAVING count(*)<>1 OR NOT bool_and("IsSynthetic"))
 THEN RAISE EXCEPTION 'Foreign synthetic province invariant failed'; END IF;

 FOREACH section IN ARRAY ARRAY['airports','commercial'] LOOP
   table_name=CASE WHEN section='airports' THEN 'airport_geography_mappings' ELSE 'snapptrip_city_mappings' END;
   key_name=CASE WHEN section='airports' THEN 'AirportIataCode' ELSE 'ProviderCityCode' END;
   FOR row IN SELECT value FROM jsonb_array_elements(payload->section) LOOP
     EXECUTE format('SELECT to_jsonb(m) FROM flights.%I m WHERE %I=$1',table_name,key_name) INTO old USING row->>'code';
     IF (old IS NOT NULL AND existing_hash IS NULL) OR (old IS NULL AND existing_hash IS NOT NULL)
     THEN RAISE EXCEPTION 'Unregistered or missing released mapping conflict in %',table_name; END IF;
     IF old IS NOT NULL AND (old->>'CityId'<>row->>'city_id' OR old->>'SourceVersion'<>row->>'source_version'
       OR old->>'SourceIdentity'<>row->>'source_identity' OR old->'Evidence'<>row->'evidence'
       OR old->>'MatchMethod'<>row->>'method' OR old->>'SourceSnapshotDate'<>row->>'snapshot_date')
     THEN RAISE EXCEPTION 'Existing mapping conflict in % for %',table_name,row->>'code'; END IF;
     IF old IS NULL THEN
       EXECUTE format('INSERT INTO flights.%I(%I,"CityId","SourceIdentity","MatchMethod","Evidence","SourceVersion","SourceSnapshotDate","CreatedAtUtc") VALUES($1,$2,$3,$4,$5,$6,$7,now())',table_name,key_name)
       USING row->>'code',(row->>'city_id')::int,row->>'source_identity',row->>'method',row->'evidence',row->>'source_version',(row->>'snapshot_date')::date;
     END IF;
   END LOOP;
 END LOOP;
 -- Preserve sequences that another application already advanced beyond this snapshot.
 PERFORM setval(pg_get_serial_sequence('"references".cities','Id'),
   greatest((SELECT max("Id") FROM "references".cities),pg_sequence_last_value(pg_get_serial_sequence('"references".cities','Id')::regclass)),true);
 PERFORM setval(pg_get_serial_sequence('"references".provinces','Id'),
   greatest((SELECT max("Id") FROM "references".provinces),pg_sequence_last_value(pg_get_serial_sequence('"references".provinces','Id')::regclass)),true);
 INSERT INTO flights.geography_releases(version,digest,receipt)
 VALUES(payload->>'version',payload->>'digest',payload-'expected_airports'-'expected_commercial'-'expected_cities'-'expected_provinces'-'expected_countries'-'expected_memberships')
 ON CONFLICT(version) DO NOTHING;
END $import$;
