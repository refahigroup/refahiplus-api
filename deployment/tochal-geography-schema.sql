-- Add the V4 runtime schema to the confirmed sparse Tochal dataset.
-- Preserve Tehran city/province ID 1. No provider mappings or reference imports.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL client_encoding = 'UTF8';
SET LOCAL lock_timeout = '15s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(174593, 20261019);

DO $target$ BEGIN
    IF current_database() <> 'tochal-db' THEN
        RAISE EXCEPTION 'This release is only for tochal-db';
    END IF;
END $target$;

LOCK TABLE "references".provinces, "references".cities IN EXCLUSIVE MODE;
DO $snapshot$ BEGIN
    IF (SELECT count(*) FROM "references".provinces) <> 1
       OR NOT EXISTS (SELECT FROM "references".provinces p WHERE to_jsonb(p) @>
           '{"Id":1,"Name":"تهران","NameEn":"Tehran","Slug":"tehran","SortOrder":1,"IsActive":true}'::jsonb)
       OR (SELECT count(*) FROM "references".cities) <> 1
       OR NOT EXISTS (SELECT FROM "references".cities c WHERE to_jsonb(c) @>
           '{"Id":1,"Name":"تهران","NameEn":"Tehran","Slug":"tehran","ProvinceId":1,"SortOrder":1,"IsActive":true}'::jsonb)
       OR EXISTS (SELECT FROM "references".provinces p
           WHERE coalesce(to_jsonb(p)->>'CountryCode', 'IR') <> 'IR'
              OR coalesce(to_jsonb(p)->>'IsSynthetic', 'false') <> 'false')
       OR EXISTS (SELECT FROM "references".cities c
           WHERE coalesce(to_jsonb(c)->>'LocationKind', 'city') <> 'city')
    THEN RAISE EXCEPTION 'Tochal geography snapshot changed; no changes applied'; END IF;
END $snapshot$;

CREATE TABLE IF NOT EXISTS "references".countries (
    "Code" varchar(2) PRIMARY KEY CHECK ("Code" ~ '^[A-Z]{2}$'),
    "Name" varchar(200) NOT NULL, "NameEn" varchar(200) NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true
);
INSERT INTO "references".countries("Code", "Name", "NameEn")
VALUES ('IR', 'ایران', 'Iran') ON CONFLICT ("Code") DO NOTHING;
DO $country$ BEGIN
    IF NOT EXISTS (SELECT FROM "references".countries
        WHERE "Code"='IR' AND "Name"='ایران' AND "NameEn"='Iran' AND "IsActive")
    THEN RAISE EXCEPTION 'Existing IR country conflicts; no changes applied'; END IF;
END $country$;

ALTER TABLE "references".provinces ADD COLUMN IF NOT EXISTS "CountryCode" varchar(2) NOT NULL DEFAULT 'IR';
ALTER TABLE "references".provinces ADD COLUMN IF NOT EXISTS "GeoNamesAdmin1Code" text;
ALTER TABLE "references".provinces ADD COLUMN IF NOT EXISTS "IsSynthetic" boolean NOT NULL DEFAULT false;
ALTER TABLE "references".cities ADD COLUMN IF NOT EXISTS "LocationKind" varchar(20) NOT NULL DEFAULT 'city';
DO $constraints$ BEGIN
    IF NOT EXISTS (SELECT FROM pg_constraint WHERE conrelid='"references".provinces'::regclass
        AND conname='fk_provinces_country') THEN
        ALTER TABLE "references".provinces ADD CONSTRAINT fk_provinces_country
            FOREIGN KEY ("CountryCode") REFERENCES "references".countries("Code");
    END IF;
END $constraints$;

CREATE OR REPLACE FUNCTION "references".city_name_key(value text) RETURNS text
LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
RETURN lower(regexp_replace(normalize(translate(value,'يك','یک'),NFKC),'[^[:alnum:]]','','g'));
CREATE UNIQUE INDEX IF NOT EXISTS ux_cities_province_normalized_name
    ON "references".cities("ProvinceId", "references".city_name_key("Name"));
CREATE UNIQUE INDEX IF NOT EXISTS ux_provinces_synthetic_country
    ON "references".provinces("CountryCode") WHERE "IsSynthetic";

-- Real audit tables: empty because no city merge or province move takes place.
CREATE TABLE IF NOT EXISTS "references".city_merge_history (
    "OldCityId" integer PRIMARY KEY,
    "CityId" integer NOT NULL REFERENCES "references".cities("Id") ON DELETE RESTRICT,
    "BeforeImage" jsonb NOT NULL, "MergedAtUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS "references".province_reorganization_history (
    "OldProvinceId" integer PRIMARY KEY,
    "ProvinceId" integer NOT NULL REFERENCES "references".provinces("Id") ON DELETE RESTRICT,
    "BeforeImage" jsonb NOT NULL, "Operation" text NOT NULL DEFAULT 'V4_country_bucket',
    "ChangedAtUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE SCHEMA IF NOT EXISTS hotels;
DO $providers$
DECLARE prefix text;
BEGIN
    FOREACH prefix IN ARRAY ARRAY['snapptrip', 'lamaso'] LOOP
        EXECUTE format('CREATE TABLE IF NOT EXISTS hotels.%I (
            "ProviderCityId" text PRIMARY KEY,
            "CityId" integer NOT NULL REFERENCES "references".cities("Id") ON DELETE RESTRICT,
            "ProviderCityName" text NOT NULL, "ProviderCityCode" text,
            "CountryCode" varchar(2) NOT NULL REFERENCES "references".countries("Code"),
            "MatchMethod" text NOT NULL, "Evidence" jsonb NOT NULL, "SourceSnapshotDate" date NOT NULL,
            "CreatedAtUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP)', prefix || '_city_mappings');
        EXECUTE format('CREATE TABLE IF NOT EXISTS hotels.%I (
            "ProviderProvinceKey" text NOT NULL,
            "KeyKind" varchar(12) NOT NULL CHECK ("KeyKind" IN (''native_id'', ''raw_value'')),
            "ProvinceId" integer NOT NULL REFERENCES "references".provinces("Id") ON DELETE RESTRICT,
            "ProviderProvinceName" text,
            "CountryCode" varchar(2) NOT NULL REFERENCES "references".countries("Code"),
            "MatchMethod" text NOT NULL, "Evidence" jsonb NOT NULL, "SourceSnapshotDate" date NOT NULL,
            "CreatedAtUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
            PRIMARY KEY ("KeyKind", "ProviderProvinceKey"))', prefix || '_province_mappings');
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON hotels.%I ("CityId")',
            prefix || '_city_mappings_city_idx', prefix || '_city_mappings');
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON hotels.%I ("ProvinceId")',
            prefix || '_province_mappings_province_idx', prefix || '_province_mappings');
    END LOOP;
END $providers$;
ALTER TABLE hotels.lamaso_city_mappings ADD COLUMN IF NOT EXISTS "IsEnabled" boolean NOT NULL DEFAULT true;

-- Flights mapping tables and EF history remain owned by the actual EF migration.
SELECT c."Id" AS city_id, c."Name" AS city_name, p."Id" AS province_id,
       p."CountryCode", co."Name" AS country_name
FROM "references".cities c JOIN "references".provinces p ON p."Id"=c."ProvinceId"
JOIN "references".countries co ON co."Code"=p."CountryCode";
COMMIT;
