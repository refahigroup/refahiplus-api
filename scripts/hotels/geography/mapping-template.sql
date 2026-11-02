-- Refahi provider geography mapping, generated 2026-10-19.
-- Reference: refahidb-14050727.sql. Provider snapshots: 2026-10-10.
-- Run this ENTIRE file in a single session. PostgreSQL 15+; UTF-8.
-- Only evidence-supported mappings are included. See mapping-report.json for omissions.
-- Does not seed/modify cities or provinces, remap Hotels keys, or enable provider search.
-- Province keys with KeyKind=raw_value are original Inventory values, NOT invented IDs.
-- No fuzzy matching runs against the target database. IDs/names are checked before writing.

BEGIN;
SET LOCAL client_encoding = 'UTF8';
SET LOCAL standard_conforming_strings = on;
SET LOCAL lock_timeout = '15s';
SELECT pg_advisory_xact_lock(174593, 20261019);

LOCK TABLE "references".cities, "references".provinces IN SHARE MODE;

CREATE TEMP TABLE expected_cities (
    id integer PRIMARY KEY, name text NOT NULL, province_id integer NOT NULL
) ON COMMIT DROP;
INSERT INTO expected_cities VALUES
@@EXPECTED_CITIES@@

CREATE TEMP TABLE expected_provinces (
    id integer PRIMARY KEY, name text NOT NULL
) ON COMMIT DROP;
INSERT INTO expected_provinces VALUES
@@EXPECTED_PROVINCES@@

DO $guard$
BEGIN
    IF EXISTS (
        SELECT 1 FROM expected_cities e
        LEFT JOIN "references".cities c ON c."Id" = e.id
        WHERE c."Id" IS NULL OR c."Name" IS DISTINCT FROM e.name
           OR c."ProvinceId" IS DISTINCT FROM e.province_id OR c."IsActive" IS DISTINCT FROM true
    ) OR EXISTS (
        SELECT 1 FROM expected_provinces e
        LEFT JOIN "references".provinces p ON p."Id" = e.id
        WHERE p."Id" IS NULL OR p."Name" IS DISTINCT FROM e.name OR p."IsActive" IS DISTINCT FROM true
    ) THEN
        RAISE EXCEPTION 'Reference geography differs from the supplied dump; nothing was applied.';
    END IF;
END
$guard$;

CREATE TABLE IF NOT EXISTS "references".provider_city_mappings (
    "Provider" varchar(30) NOT NULL CHECK ("Provider" IN ('SnappTrip', 'Lamasoo')),
    "ProviderCityId" text NOT NULL CHECK (length("ProviderCityId") > 0),
    "CityId" integer NOT NULL REFERENCES "references".cities("Id") ON DELETE RESTRICT,
    "ProviderCityName" text NOT NULL,
    "ProviderCityCode" text,
    "CountryCode" varchar(2) NOT NULL DEFAULT 'IR' CHECK ("CountryCode" = 'IR'),
    "MatchMethod" text NOT NULL,
    "Evidence" jsonb NOT NULL,
    "SourceSnapshotDate" date NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY ("Provider", "ProviderCityId")
);
CREATE INDEX IF NOT EXISTS ix_provider_city_mappings_city
    ON "references".provider_city_mappings ("CityId", "Provider");

CREATE TABLE IF NOT EXISTS "references".provider_province_mappings (
    "Provider" varchar(30) NOT NULL CHECK ("Provider" IN ('SnappTrip', 'Lamasoo')),
    "ProviderProvinceKey" text NOT NULL CHECK (length("ProviderProvinceKey") > 0),
    "KeyKind" varchar(12) NOT NULL CHECK ("KeyKind" IN ('native_id', 'raw_value')),
    "ProvinceId" integer NOT NULL REFERENCES "references".provinces("Id") ON DELETE RESTRICT,
    "ProviderProvinceName" text,
    "CountryCode" varchar(2) NOT NULL DEFAULT 'IR' CHECK ("CountryCode" = 'IR'),
    "MatchMethod" text NOT NULL,
    "Evidence" jsonb NOT NULL,
    "SourceSnapshotDate" date NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY ("Provider", "KeyKind", "ProviderProvinceKey")
);
CREATE INDEX IF NOT EXISTS ix_provider_province_mappings_province
    ON "references".provider_province_mappings ("ProvinceId", "Provider");

LOCK TABLE "references".provider_city_mappings, "references".provider_province_mappings
    IN SHARE ROW EXCLUSIVE MODE;

CREATE TEMP TABLE staged_city_mappings (
    provider text, external_id text, city_id integer, name text, code text, method text, evidence jsonb,
    PRIMARY KEY(provider, external_id)
) ON COMMIT DROP;
INSERT INTO staged_city_mappings VALUES
@@CITY_MAPPINGS@@

CREATE TEMP TABLE staged_province_mappings (
    provider text, external_key text, key_kind text, province_id integer, name text, method text, evidence jsonb,
    PRIMARY KEY(provider, key_kind, external_key)
) ON COMMIT DROP;
INSERT INTO staged_province_mappings VALUES
@@PROVINCE_MAPPINGS@@

DO $guard$
BEGIN
    IF EXISTS (
        SELECT 1 FROM staged_city_mappings s
        JOIN "references".provider_city_mappings m ON m."Provider" = s.provider AND m."ProviderCityId" = s.external_id
        WHERE m."CityId" IS DISTINCT FROM s.city_id OR m."CountryCode" IS DISTINCT FROM 'IR'
    ) OR EXISTS (
        SELECT 1 FROM staged_province_mappings s
        JOIN "references".provider_province_mappings m ON m."Provider" = s.provider
            AND m."KeyKind" = s.key_kind AND m."ProviderProvinceKey" = s.external_key
        WHERE m."ProvinceId" IS DISTINCT FROM s.province_id OR m."CountryCode" IS DISTINCT FROM 'IR'
    ) THEN
        RAISE EXCEPTION 'An existing provider mapping conflicts with this snapshot; no mapping was overwritten.';
    END IF;
END
$guard$;

INSERT INTO "references".provider_city_mappings (
    "Provider", "ProviderCityId", "CityId", "ProviderCityName", "ProviderCityCode", "CountryCode",
    "MatchMethod", "Evidence", "SourceSnapshotDate"
)
SELECT provider, external_id, city_id, name, code, 'IR', method, evidence, DATE '2026-10-10'
FROM staged_city_mappings
ON CONFLICT ("Provider", "ProviderCityId") DO NOTHING;

INSERT INTO "references".provider_province_mappings (
    "Provider", "ProviderProvinceKey", "KeyKind", "ProvinceId", "ProviderProvinceName", "CountryCode",
    "MatchMethod", "Evidence", "SourceSnapshotDate"
)
SELECT provider, external_key, key_kind, province_id, name, 'IR', method, evidence, DATE '2026-10-10'
FROM staged_province_mappings
ON CONFLICT ("Provider", "KeyKind", "ProviderProvinceKey") DO NOTHING;

-- Counts contributed by THIS snapshot, including rows already present on a repeat run.
SELECT 'city' AS kind, provider, count(*) AS mapped_count FROM staged_city_mappings GROUP BY provider
UNION ALL
SELECT 'province', provider, count(*) FROM staged_province_mappings GROUP BY provider
ORDER BY kind, provider;

COMMIT;
