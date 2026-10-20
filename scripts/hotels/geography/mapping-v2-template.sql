-- V2, 2026-10-19. Supersedes the V1 mapping-only script.
-- Run ENTIRE file with ON_ERROR_STOP, in one session, during a maintenance window.
-- Adds countries/provinces/destinations, migrates city references, then deletes duplicates.
-- GeoNames geographic evidence: CC BY 4.0, https://www.geonames.org/.
-- Existing business tables are not renamed. See provider-table-naming-audit.md.
BEGIN;
SET LOCAL client_encoding = 'UTF8';
SET LOCAL standard_conforming_strings = on;
SET LOCAL lock_timeout = '15s';
SELECT pg_advisory_xact_lock(174593, 20261019);
LOCK TABLE "references".cities, "references".provinces IN EXCLUSIVE MODE;

CREATE TEMP TABLE original_cities(id integer PRIMARY KEY,name text,province_id integer) ON COMMIT DROP;
INSERT INTO original_cities VALUES
@@ORIGINAL_CITIES@@
CREATE TEMP TABLE original_provinces(id integer PRIMARY KEY,name text) ON COMMIT DROP;
INSERT INTO original_provinces VALUES
@@ORIGINAL_PROVINCES@@
CREATE TEMP TABLE city_merges(old_id integer PRIMARY KEY,new_id integer,before_image jsonb) ON COMMIT DROP;
INSERT INTO city_merges VALUES
@@MERGES@@

CREATE TABLE IF NOT EXISTS "references".city_merge_history (
    "OldCityId" integer PRIMARY KEY,
    "CityId" integer NOT NULL REFERENCES "references".cities("Id") ON DELETE RESTRICT,
    "BeforeImage" jsonb NOT NULL,
    "MergedAtUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);
DO $guard$
BEGIN
    IF EXISTS(SELECT 1 FROM original_cities e LEFT JOIN "references".cities c ON c."Id"=e.id
        LEFT JOIN "references".city_merge_history h ON h."OldCityId"=e.id
        LEFT JOIN city_merges m ON m.old_id=e.id
        WHERE (c."Id" IS NULL AND (h."CityId" IS NULL OR h."CityId" IS DISTINCT FROM m.new_id))
           OR (c."Id" IS NOT NULL AND (c."Name" IS DISTINCT FROM e.name OR c."ProvinceId" IS DISTINCT FROM e.province_id)))
       OR EXISTS(SELECT 1 FROM original_provinces e LEFT JOIN "references".provinces p ON p."Id"=e.id
           WHERE p."Id" IS NULL OR p."Name" IS DISTINCT FROM e.name)
       OR EXISTS(SELECT 1 FROM city_merges m JOIN "references".city_merge_history h ON h."OldCityId"=m.old_id
           WHERE h."CityId"<>m.new_id)
    THEN RAISE EXCEPTION 'Reference snapshot or merge history conflicts; no changes were applied.'; END IF;
END
$guard$;

-- Discover declared references. Composite foreign keys need an explicit migration.
CREATE TEMP TABLE city_reference_columns(schema_name text,table_name text,column_name text,
    PRIMARY KEY(schema_name,table_name,column_name)) ON COMMIT DROP;
DO $references$
DECLARE r record;
BEGIN
    IF EXISTS(SELECT 1 FROM pg_constraint WHERE contype='f' AND confrelid='"references".cities'::regclass
              AND array_length(conkey,1)<>1)
    THEN RAISE EXCEPTION 'Composite city FK found; explicit migration is required.'; END IF;
    INSERT INTO city_reference_columns
    SELECT n.nspname,t.relname,a.attname FROM pg_constraint c
    JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
    JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=c.conkey[1]
    WHERE c.contype='f' AND c.confrelid='"references".cities'::regclass
    ON CONFLICT DO NOTHING;
    -- Known logical references, identified from the application model. Native hotel city IDs are excluded.
    FOR r IN SELECT * FROM (VALUES
        ('identity','user_addresses','city_id'),('store','shops','CityId'),('supplychain','suppliers','CityId')
    ) AS x(schema_name,table_name,column_name) LOOP
        IF EXISTS(SELECT 1 FROM information_schema.columns c WHERE c.table_schema=r.schema_name
                  AND c.table_name=r.table_name AND c.column_name=r.column_name) THEN
            INSERT INTO city_reference_columns VALUES(r.schema_name,r.table_name,r.column_name) ON CONFLICT DO NOTHING;
        END IF;
    END LOOP;
    FOR r IN SELECT * FROM city_reference_columns ORDER BY schema_name,table_name,column_name LOOP
        EXECUTE format('LOCK TABLE %I.%I IN SHARE ROW EXCLUSIVE MODE',r.schema_name,r.table_name);
        EXECUTE format('UPDATE %I.%I t SET %I=m.new_id FROM city_merges m WHERE t.%I=m.old_id',
                       r.schema_name,r.table_name,r.column_name,r.column_name);
    END LOOP;
END
$references$;
INSERT INTO "references".city_merge_history("OldCityId","CityId","BeforeImage")
SELECT old_id,new_id,before_image FROM city_merges ON CONFLICT("OldCityId") DO NOTHING;
DELETE FROM "references".cities c USING city_merges m WHERE c."Id"=m.old_id;

-- Stable public normalization rule for the unique city+province index.
CREATE OR REPLACE FUNCTION "references".city_name_key(value text) RETURNS text
LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
RETURN lower(regexp_replace(normalize(translate(value,'يك','یک'),NFKC),'[^[:alnum:]]','','g'));

CREATE TABLE IF NOT EXISTS "references".countries (
    "Code" varchar(2) PRIMARY KEY CHECK("Code" ~ '^[A-Z]{2}$'),
    "Name" varchar(200) NOT NULL,"NameEn" varchar(200) NOT NULL,"IsActive" boolean NOT NULL DEFAULT true
);
CREATE TEMP TABLE staged_countries(code text PRIMARY KEY,name text,name_en text) ON COMMIT DROP;
INSERT INTO staged_countries VALUES
@@COUNTRIES@@
INSERT INTO "references".countries("Code","Name","NameEn") SELECT code,name,name_en FROM staged_countries
ON CONFLICT("Code") DO NOTHING;

ALTER TABLE "references".provinces ADD COLUMN IF NOT EXISTS "CountryCode" varchar(2) NOT NULL DEFAULT 'IR';
ALTER TABLE "references".provinces ADD COLUMN IF NOT EXISTS "GeoNamesAdmin1Code" text;
ALTER TABLE "references".cities ADD COLUMN IF NOT EXISTS "LocationKind" varchar(20) NOT NULL DEFAULT 'city';
DO $constraints$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid='"references".provinces'::regclass AND conname='fk_provinces_country') THEN
        ALTER TABLE "references".provinces ADD CONSTRAINT fk_provinces_country FOREIGN KEY("CountryCode") REFERENCES "references".countries("Code");
    END IF;
END
$constraints$;
CREATE TEMP TABLE staged_provinces(id integer PRIMARY KEY,name text,name_en text,slug text,country text,admin1 text,sort_order integer) ON COMMIT DROP;
INSERT INTO staged_provinces VALUES
@@PROVINCES@@
DO $guard$
BEGIN
    IF EXISTS(SELECT 1 FROM staged_provinces s JOIN "references".provinces p ON p."Id"=s.id
        WHERE p."Name" IS DISTINCT FROM s.name OR p."CountryCode" IS DISTINCT FROM s.country
           OR (p."GeoNamesAdmin1Code" IS NOT NULL AND p."GeoNamesAdmin1Code" IS DISTINCT FROM s.admin1))
    THEN RAISE EXCEPTION 'Province ID/country conflict; regenerate the script from a current dump.'; END IF;
END
$guard$;
INSERT INTO "references".provinces("Id","Name","NameEn","Slug","SortOrder","IsActive","CountryCode","GeoNamesAdmin1Code")
SELECT id,name,name_en,slug,sort_order,true,country,admin1 FROM staged_provinces ON CONFLICT("Id") DO NOTHING;
UPDATE "references".provinces p SET "GeoNamesAdmin1Code"=s.admin1 FROM staged_provinces s
WHERE p."Id"=s.id AND p."GeoNamesAdmin1Code" IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_provinces_geonames_admin1 ON "references".provinces("GeoNamesAdmin1Code") WHERE "GeoNamesAdmin1Code" IS NOT NULL;

CREATE TEMP TABLE staged_new_cities(id integer PRIMARY KEY,name text,name_en text,slug text,province_id integer,sort_order integer,kind text) ON COMMIT DROP;
INSERT INTO staged_new_cities VALUES
@@NEW_CITIES@@
DO $guard$
BEGIN
    IF EXISTS(SELECT 1 FROM staged_new_cities s JOIN "references".cities c ON c."Id"=s.id
              WHERE c."Name" IS DISTINCT FROM s.name OR c."ProvinceId" IS DISTINCT FROM s.province_id OR c."Slug" IS DISTINCT FROM s.slug)
       OR EXISTS(SELECT 1 FROM staged_new_cities s JOIN "references".cities c ON c."ProvinceId"=s.province_id
           AND "references".city_name_key(c."Name")="references".city_name_key(s.name) WHERE c."Id"<>s.id)
    THEN RAISE EXCEPTION 'New city ID/name conflicts with live data; regenerate the script.'; END IF;
END
$guard$;
INSERT INTO "references".cities("Id","Name","NameEn","Slug","ProvinceId","SortOrder","IsActive","LocationKind")
SELECT id,name,name_en,slug,province_id,sort_order,true,kind FROM staged_new_cities ON CONFLICT("Id") DO NOTHING;
CREATE UNIQUE INDEX IF NOT EXISTS ux_cities_province_normalized_name
ON "references".cities("ProvinceId","references".city_name_key("Name"));

CREATE SCHEMA IF NOT EXISTS hotels;
-- Each provider owns separate tables in Hotels. No Provider discriminator is needed in these tables.
DO $tables$
DECLARE prefix text;
BEGIN
    FOREACH prefix IN ARRAY ARRAY['snapptrip','lamaso'] LOOP
        EXECUTE format('CREATE TABLE IF NOT EXISTS hotels.%I (
            "ProviderCityId" text PRIMARY KEY,"CityId" integer NOT NULL REFERENCES "references".cities("Id") ON DELETE RESTRICT,
            "ProviderCityName" text NOT NULL,"ProviderCityCode" text,"CountryCode" varchar(2) NOT NULL REFERENCES "references".countries("Code"),
            "MatchMethod" text NOT NULL,"Evidence" jsonb NOT NULL,"SourceSnapshotDate" date NOT NULL,
            "CreatedAtUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP)',prefix||'_city_mappings');
        EXECUTE format('CREATE TABLE IF NOT EXISTS hotels.%I (
            "ProviderProvinceKey" text NOT NULL,"KeyKind" varchar(12) NOT NULL CHECK("KeyKind" IN (''native_id'',''raw_value'')),
            "ProvinceId" integer NOT NULL REFERENCES "references".provinces("Id") ON DELETE RESTRICT,
            "ProviderProvinceName" text,"CountryCode" varchar(2) NOT NULL REFERENCES "references".countries("Code"),
            "MatchMethod" text NOT NULL,"Evidence" jsonb NOT NULL,"SourceSnapshotDate" date NOT NULL,
            "CreatedAtUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,PRIMARY KEY("KeyKind","ProviderProvinceKey"))',prefix||'_province_mappings');
        EXECUTE format('LOCK TABLE hotels.%I,hotels.%I IN SHARE ROW EXCLUSIVE MODE',prefix||'_city_mappings',prefix||'_province_mappings');
    END LOOP;
END
$tables$;

-- If V1 was applied, move its data before dropping the incorrectly owned generic tables.
DO $legacy$
DECLARE kind text; r record; target text; existing_id integer;
BEGIN
    FOREACH kind IN ARRAY ARRAY['city','province'] LOOP
        IF to_regclass(format('"references".provider_%s_mappings',kind)) IS NULL THEN CONTINUE; END IF;
        EXECUTE format('LOCK TABLE "references".provider_%s_mappings IN SHARE ROW EXCLUSIVE MODE',kind);
        FOR r IN EXECUTE format('SELECT to_jsonb(m) AS data FROM "references".provider_%s_mappings m',kind) LOOP
            target=CASE r.data->>'Provider' WHEN 'SnappTrip' THEN 'snapptrip' WHEN 'Lamasoo' THEN 'lamaso' ELSE NULL END;
            IF target IS NULL THEN RAISE EXCEPTION 'Unknown provider in V1 mappings; no table was dropped.'; END IF;
            IF kind='city' THEN
                EXECUTE format('SELECT "CityId" FROM hotels.%I WHERE "ProviderCityId"=$1',target||'_city_mappings')
                    INTO existing_id USING r.data->>'ProviderCityId';
                IF existing_id IS NOT NULL AND existing_id<>(r.data->>'CityId')::integer THEN RAISE EXCEPTION 'Legacy city mapping conflicts.'; END IF;
                EXECUTE format('INSERT INTO hotels.%I ("ProviderCityId","CityId","ProviderCityName","ProviderCityCode","CountryCode","MatchMethod","Evidence","SourceSnapshotDate","CreatedAtUtc")
                    VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9) ON CONFLICT("ProviderCityId") DO UPDATE SET "Evidence"=hotels.%I."Evidence" || jsonb_build_object(''V1BeforeImage'',$10)',target||'_city_mappings',target||'_city_mappings')
                    USING r.data->>'ProviderCityId',(r.data->>'CityId')::integer,r.data->>'ProviderCityName',r.data->>'ProviderCityCode',r.data->>'CountryCode',
                    r.data->>'MatchMethod',(r.data->'Evidence')||jsonb_build_object('V1BeforeImage',r.data),(r.data->>'SourceSnapshotDate')::date,(r.data->>'CreatedAtUtc')::timestamptz,r.data;
            ELSE
                EXECUTE format('SELECT "ProvinceId" FROM hotels.%I WHERE "ProviderProvinceKey"=$1 AND "KeyKind"=$2',target||'_province_mappings')
                    INTO existing_id USING r.data->>'ProviderProvinceKey',r.data->>'KeyKind';
                IF existing_id IS NOT NULL AND existing_id<>(r.data->>'ProvinceId')::integer THEN RAISE EXCEPTION 'Legacy province mapping conflicts.'; END IF;
                EXECUTE format('INSERT INTO hotels.%I ("ProviderProvinceKey","KeyKind","ProvinceId","ProviderProvinceName","CountryCode","MatchMethod","Evidence","SourceSnapshotDate","CreatedAtUtc")
                    VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9) ON CONFLICT("KeyKind","ProviderProvinceKey") DO UPDATE SET "Evidence"=hotels.%I."Evidence" || jsonb_build_object(''V1BeforeImage'',$10)',target||'_province_mappings',target||'_province_mappings')
                    USING r.data->>'ProviderProvinceKey',r.data->>'KeyKind',(r.data->>'ProvinceId')::integer,r.data->>'ProviderProvinceName',r.data->>'CountryCode',
                    r.data->>'MatchMethod',(r.data->'Evidence')||jsonb_build_object('V1BeforeImage',r.data),(r.data->>'SourceSnapshotDate')::date,(r.data->>'CreatedAtUtc')::timestamptz,r.data;
            END IF;
        END LOOP;
        EXECUTE format('DROP TABLE "references".provider_%s_mappings',kind);
    END LOOP;
END
$legacy$;

CREATE TEMP TABLE staged_city_mappings(provider text,external_id text,city_id integer,name text,code text,country text,method text,evidence jsonb,
    PRIMARY KEY(provider,external_id)) ON COMMIT DROP;
INSERT INTO staged_city_mappings VALUES
@@CITY_MAPPINGS@@
CREATE TEMP TABLE staged_province_mappings(provider text,external_key text,kind text,province_id integer,evidence jsonb,
    PRIMARY KEY(provider,kind,external_key)) ON COMMIT DROP;
INSERT INTO staged_province_mappings VALUES
@@PROVINCE_MAPPINGS@@
DO $mappings$
DECLARE p text; prefix text;
BEGIN
    FOREACH p IN ARRAY ARRAY['SnappTrip','Lamasoo'] LOOP
        prefix=CASE p WHEN 'SnappTrip' THEN 'snapptrip' ELSE 'lamaso' END;
        EXECUTE format('LOCK TABLE hotels.%I,hotels.%I IN SHARE ROW EXCLUSIVE MODE',prefix||'_city_mappings',prefix||'_province_mappings');
        IF EXISTS(SELECT 1 FROM staged_city_mappings s JOIN "references".cities c ON c."Id"=s.city_id
                  JOIN "references".provinces pr ON pr."Id"=c."ProvinceId" WHERE s.provider=p AND pr."CountryCode"<>s.country)
        THEN RAISE EXCEPTION 'City country mismatch.'; END IF;
        EXECUTE format('DO $check$ BEGIN IF EXISTS(SELECT 1 FROM staged_city_mappings s JOIN hotels.%I m ON m."ProviderCityId"=s.external_id
            WHERE s.provider=%L AND (m."CityId"<>s.city_id OR m."CountryCode"<>s.country))
            THEN RAISE EXCEPTION ''Existing city mapping conflicts.''; END IF; END $check$',prefix||'_city_mappings',p);
        EXECUTE format('DO $check$ BEGIN IF EXISTS(SELECT 1 FROM staged_province_mappings s JOIN hotels.%I m ON m."ProviderProvinceKey"=s.external_key AND m."KeyKind"=s.kind
            WHERE s.provider=%L AND m."ProvinceId"<>s.province_id)
            THEN RAISE EXCEPTION ''Existing province mapping conflicts.''; END IF; END $check$',prefix||'_province_mappings',p);
        EXECUTE format('INSERT INTO hotels.%I ("ProviderCityId","CityId","ProviderCityName","ProviderCityCode","CountryCode","MatchMethod","Evidence","SourceSnapshotDate")
            SELECT external_id,city_id,name,code,country,method,evidence,DATE ''2026-10-10'' FROM staged_city_mappings WHERE provider=$1 ON CONFLICT("ProviderCityId") DO NOTHING',prefix||'_city_mappings') USING p;
        EXECUTE format('INSERT INTO hotels.%I ("ProviderProvinceKey","KeyKind","ProvinceId","CountryCode","MatchMethod","Evidence","SourceSnapshotDate")
            SELECT s.external_key,s.kind,s.province_id,pr."CountryCode",''verified_observations'',s.evidence,DATE ''2026-10-10'' FROM staged_province_mappings s
            JOIN "references".provinces pr ON pr."Id"=s.province_id WHERE s.provider=$1 ON CONFLICT("KeyKind","ProviderProvinceKey") DO NOTHING',prefix||'_province_mappings') USING p;
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON hotels.%I("CityId")',prefix||'_city_mappings_city_idx',prefix||'_city_mappings');
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON hotels.%I("ProvinceId")',prefix||'_province_mappings_province_idx',prefix||'_province_mappings');
    END LOOP;
END
$mappings$;
-- Preserve high-water marks; the supplied dump reset these sequences to 1.
DO $sequences$
DECLARE t text; seq text; maximum_id bigint; last_id bigint;
BEGIN
    FOREACH t IN ARRAY ARRAY['cities','provinces'] LOOP
        seq=pg_get_serial_sequence(format('"references".%I',t),'Id');
        IF seq IS NULL THEN RAISE EXCEPTION 'Missing identity/serial sequence for %',t; END IF;
        EXECUTE format('SELECT max("Id") FROM "references".%I',t) INTO maximum_id;
        EXECUTE format('SELECT last_value FROM %s',seq) INTO last_id;
        PERFORM setval(seq::regclass,greatest(maximum_id,last_id),true);
    END LOOP;
END
$sequences$;
SELECT 'cities' AS kind,provider,count(*) AS count FROM staged_city_mappings GROUP BY provider
UNION ALL SELECT 'provinces',provider,count(*) FROM staged_province_mappings GROUP BY provider ORDER BY kind,provider;
SELECT (SELECT count(*) FROM staged_new_cities) AS cities_added,(SELECT count(*) FROM city_merges) AS duplicate_cities_removed;
COMMIT;
