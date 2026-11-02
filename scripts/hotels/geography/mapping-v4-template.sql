-- V4 upgrade ONLY. Apply V3 first; do not replay V1/V2/V3 after V4.
-- Execute the entire UTF-8 file in one session with ON_ERROR_STOP=1.
BEGIN;
SET LOCAL lock_timeout='15s';
SET LOCAL standard_conforming_strings=on;
SET LOCAL client_encoding='UTF8';
SELECT pg_advisory_xact_lock(174593,20261019);
DO $check$ BEGIN
 IF to_regclass('hotels.lamaso_city_mappings') IS NULL OR to_regclass('"references".countries') IS NULL
 THEN RAISE EXCEPTION 'Apply V3 reference geography first.'; END IF;
END $check$;
LOCK TABLE "references".countries,"references".provinces,"references".cities,
 hotels.lamaso_city_mappings,hotels.lamaso_province_mappings,
 hotels.snapptrip_city_mappings,hotels.snapptrip_province_mappings IN EXCLUSIVE MODE;
ALTER TABLE "references".provinces ADD COLUMN IF NOT EXISTS "IsSynthetic" boolean NOT NULL DEFAULT false;
ALTER TABLE hotels.lamaso_city_mappings ADD COLUMN IF NOT EXISTS "IsEnabled" boolean NOT NULL DEFAULT true;
CREATE TABLE IF NOT EXISTS "references".province_reorganization_history(
 "OldProvinceId" integer PRIMARY KEY,"ProvinceId" integer NOT NULL REFERENCES "references".provinces("Id") ON DELETE RESTRICT,
 "BeforeImage" jsonb NOT NULL,"Operation" text NOT NULL DEFAULT 'V4_country_bucket',
 "ChangedAtUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE TEMP TABLE v4_countries(code text PRIMARY KEY,name text,name_en text) ON COMMIT DROP;
INSERT INTO v4_countries VALUES
@@COUNTRIES@@
CREATE TEMP TABLE v4_buckets(id integer PRIMARY KEY,country text UNIQUE,name text,name_en text) ON COMMIT DROP;
INSERT INTO v4_buckets VALUES
@@BUCKETS@@
CREATE TEMP TABLE v4_new_buckets(id integer PRIMARY KEY,country text UNIQUE,name text,name_en text) ON COMMIT DROP;
INSERT INTO v4_new_buckets VALUES
@@NEW_BUCKETS@@
CREATE TEMP TABLE v4_provinces(old_id integer PRIMARY KEY,old_name text,country text,target_id integer) ON COMMIT DROP;
INSERT INTO v4_provinces VALUES
@@PROVINCES@@
DO $guard$ BEGIN
 IF NOT EXISTS(SELECT FROM "references".cities WHERE "Id"=1414 AND "Name"='راس الخیمه')
 THEN RAISE EXCEPTION 'V3 data is missing.'; END IF;
 IF EXISTS(SELECT FROM v4_provinces s LEFT JOIN "references".provinces p ON p."Id"=s.old_id
   LEFT JOIN "references".province_reorganization_history h ON h."OldProvinceId"=s.old_id
   WHERE (h."OldProvinceId" IS NULL AND (p."Id" IS NULL OR p."Name"<>s.old_name OR p."CountryCode"<>s.country))
    OR (h."OldProvinceId" IS NOT NULL AND (h."ProvinceId"<>s.target_id OR h."BeforeImage"->>'Name'<>s.old_name
       OR h."BeforeImage"->>'CountryCode'<>s.country OR (p."Id" IS NOT NULL AND (s.old_id<>s.target_id OR p."CountryCode"<>s.country)))))
 THEN RAISE EXCEPTION 'Foreign province snapshot/history conflicts; regenerate from current data.'; END IF;
 IF EXISTS(SELECT FROM "references".provinces p WHERE p."CountryCode"<>'IR'
    AND NOT EXISTS(SELECT FROM v4_provinces s WHERE s.old_id=p."Id")
    AND NOT EXISTS(SELECT FROM v4_new_buckets b WHERE b.id=p."Id" AND b.country=p."CountryCode" AND p."IsSynthetic"))
 THEN RAISE EXCEPTION 'Unreviewed foreign province found.'; END IF;
 IF EXISTS(SELECT FROM v4_new_buckets b JOIN "references".provinces p ON p."Id"=b.id
   WHERE p."CountryCode"<>b.country OR NOT p."IsSynthetic" OR p."Name"<>b.name)
 THEN RAISE EXCEPTION 'New country bucket ID conflicts.'; END IF;
 IF EXISTS(SELECT FROM v4_buckets b JOIN "references".provinces p ON p."Id"=b.id
   JOIN "references".province_reorganization_history h ON h."OldProvinceId"=b.id
   WHERE p."CountryCode"<>b.country OR NOT p."IsSynthetic" OR p."Name"<>b.name)
 THEN RAISE EXCEPTION 'Existing country bucket changed after V4.'; END IF;
 IF EXISTS(SELECT "references".city_name_key(c."Name"),coalesce(s.target_id,c."ProvinceId") FROM "references".cities c
   LEFT JOIN v4_provinces s ON s.old_id=c."ProvinceId"
   GROUP BY "references".city_name_key(c."Name"),coalesce(s.target_id,c."ProvinceId") HAVING count(*)>1)
 THEN RAISE EXCEPTION 'City names collide after province consolidation; review before merging.'; END IF;
END $guard$;
INSERT INTO "references".countries("Code","Name","NameEn") SELECT code,name,name_en FROM v4_countries ON CONFLICT("Code") DO NOTHING;
UPDATE "references".countries c SET "Name"=s.name,"NameEn"=s.name_en FROM v4_countries s WHERE c."Code"=s.code;
INSERT INTO "references".provinces("Id","Name","NameEn","Slug","SortOrder","IsActive","CountryCode","IsSynthetic")
 SELECT id,name,name_en,'country-'||lower(country),id,true,country,true FROM v4_new_buckets ON CONFLICT("Id") DO NOTHING;
INSERT INTO "references".province_reorganization_history("OldProvinceId","ProvinceId","BeforeImage")
 SELECT s.old_id,s.target_id,to_jsonb(p) FROM v4_provinces s JOIN "references".provinces p ON p."Id"=s.old_id
 ON CONFLICT("OldProvinceId") DO NOTHING;
-- Keep the former provider mapping rows before redirecting their province references.
DO $evidence$ DECLARE prefix text; BEGIN
 FOREACH prefix IN ARRAY ARRAY['lamaso','snapptrip'] LOOP
  EXECUTE format('UPDATE hotels.%I m SET "Evidence"=m."Evidence"||jsonb_build_object(''V4BeforeImage'',to_jsonb(m))
   FROM v4_provinces s WHERE m."ProvinceId"=s.old_id AND NOT(m."Evidence" ? ''V4BeforeImage'')',prefix||'_province_mappings');
 END LOOP;
END $evidence$;
CREATE TEMP TABLE v4_province_references(schema_name text,table_name text,column_name text,PRIMARY KEY(schema_name,table_name,column_name)) ON COMMIT DROP;
DO $references$ DECLARE r record; BEGIN
 IF EXISTS(SELECT FROM pg_constraint WHERE contype='f' AND confrelid='"references".provinces'::regclass AND array_length(conkey,1)<>1)
 THEN RAISE EXCEPTION 'Composite province FK requires explicit migration.'; END IF;
 INSERT INTO v4_province_references SELECT n.nspname,t.relname,a.attname FROM pg_constraint c
 JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
 JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=c.conkey[1]
 WHERE c.contype='f' AND c.confrelid='"references".provinces'::regclass ON CONFLICT DO NOTHING;
 FOR r IN SELECT * FROM (VALUES ('identity','user_addresses','province_id'),('store','shops','ProvinceId'),('supplychain','suppliers','ProvinceId')) x(schema_name,table_name,column_name) LOOP
  IF EXISTS(SELECT FROM information_schema.columns c WHERE c.table_schema=r.schema_name AND c.table_name=r.table_name AND c.column_name=r.column_name)
  THEN INSERT INTO v4_province_references VALUES(r.schema_name,r.table_name,r.column_name) ON CONFLICT DO NOTHING; END IF;
 END LOOP;
 FOR r IN SELECT * FROM v4_province_references ORDER BY schema_name,table_name,column_name LOOP
  EXECUTE format('LOCK TABLE %I.%I IN SHARE ROW EXCLUSIVE MODE',r.schema_name,r.table_name);
  EXECUTE format('UPDATE %I.%I t SET %I=s.target_id FROM v4_provinces s WHERE t.%I=s.old_id AND s.old_id<>s.target_id',r.schema_name,r.table_name,r.column_name,r.column_name);
 END LOOP;
END $references$;
DELETE FROM "references".provinces p USING v4_provinces s WHERE p."Id"=s.old_id AND s.old_id<>s.target_id;
UPDATE "references".provinces p SET "Name"=b.name,"NameEn"=b.name_en,"IsSynthetic"=true,"GeoNamesAdmin1Code"=NULL
 FROM v4_buckets b WHERE p."Id"=b.id AND p."CountryCode"=b.country;
CREATE UNIQUE INDEX IF NOT EXISTS ux_provinces_synthetic_country ON "references".provinces("CountryCode") WHERE "IsSynthetic";
CREATE TEMP TABLE v4_new_cities(id integer PRIMARY KEY,name text,name_en text,province_id integer,slug text,sort_order integer,kind text) ON COMMIT DROP;
INSERT INTO v4_new_cities VALUES
@@NEW_CITIES@@
DO $guard$ BEGIN
 IF EXISTS(SELECT FROM v4_new_cities s JOIN "references".cities c ON c."Id"=s.id WHERE c."Name"<>s.name OR c."ProvinceId"<>s.province_id OR c."Slug"<>s.slug)
 OR EXISTS(SELECT FROM v4_new_cities s JOIN "references".cities c ON c."ProvinceId"=s.province_id AND "references".city_name_key(c."Name")="references".city_name_key(s.name) WHERE c."Id"<>s.id)
 THEN RAISE EXCEPTION 'New confirmed city ID/name conflicts.'; END IF;
END $guard$;
INSERT INTO "references".cities("Id","Name","NameEn","ProvinceId","Slug","SortOrder","IsActive","LocationKind")
 SELECT id,name,name_en,province_id,slug,sort_order,true,kind FROM v4_new_cities ON CONFLICT("Id") DO NOTHING;
CREATE TEMP TABLE v4_mappings(native_id text PRIMARY KEY,city_id integer,name text,code text,country text,enabled boolean,evidence jsonb,canonical_name text,province_id integer) ON COMMIT DROP;
INSERT INTO v4_mappings VALUES
@@MAPPINGS@@
DO $guard$ BEGIN
 IF EXISTS(SELECT FROM v4_mappings s LEFT JOIN "references".cities c ON c."Id"=s.city_id
   LEFT JOIN "references".provinces p ON p."Id"=c."ProvinceId" WHERE c."Id" IS NULL OR c."Name"<>s.canonical_name OR c."ProvinceId"<>s.province_id OR p."CountryCode"<>s.country)
 THEN RAISE EXCEPTION 'Confirmed city/country/province mismatch.'; END IF;
 IF EXISTS(SELECT FROM v4_mappings s JOIN hotels.lamaso_city_mappings m ON m."ProviderCityId"=s.native_id
    WHERE m."CityId"<>s.city_id OR m."CountryCode"<>s.country OR m."IsEnabled"<>s.enabled)
 THEN RAISE EXCEPTION 'Existing provider mapping conflicts with user decision.'; END IF;
 IF EXISTS(SELECT FROM "references".provinces WHERE "CountryCode"<>'IR' GROUP BY "CountryCode" HAVING count(*)<>1)
 OR EXISTS(SELECT FROM "references".provinces WHERE "CountryCode"='IR' AND "IsSynthetic")
 THEN RAISE EXCEPTION 'Country bucket policy violated.'; END IF;
END $guard$;
INSERT INTO hotels.lamaso_city_mappings("ProviderCityId","CityId","ProviderCityName","ProviderCityCode","CountryCode","MatchMethod","Evidence","SourceSnapshotDate","IsEnabled")
 SELECT native_id,city_id,name,code,country,'explicit_user_decision',evidence,DATE '2026-10-19',enabled FROM v4_mappings
 ON CONFLICT("ProviderCityId") DO NOTHING;
DO $sequences$ DECLARE t text; seq text; maximum_id bigint; last_id bigint; BEGIN
 FOREACH t IN ARRAY ARRAY['cities','provinces'] LOOP
  seq=pg_get_serial_sequence(format('"references".%I',t),'Id');
  IF seq IS NULL THEN RAISE EXCEPTION 'Missing identity sequence.'; END IF;
  EXECUTE format('SELECT max("Id") FROM "references".%I',t) INTO maximum_id;
  EXECUTE format('SELECT last_value FROM %s',seq) INTO last_id;
  PERFORM setval(seq::regclass,greatest(maximum_id,last_id),true);
 END LOOP;
END $sequences$;
SELECT count(*) AS confirmed_mappings,count(*) FILTER(WHERE NOT enabled) AS disabled_identity_mappings FROM v4_mappings;
COMMIT;
