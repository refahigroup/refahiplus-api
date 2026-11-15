-- Run after stopping Commerce writes. This file only reports counts and runs read-only.
BEGIN READ ONLY;
DO $$
DECLARE t record; rows bigint;
BEGIN
    IF to_regclass('orders.orders') IS NOT NULL THEN
        SELECT count(*) INTO rows FROM orders.orders
        WHERE lower(source_module) = 'commerce' OR lower(reference_type) = 'commerceorder';
        RAISE NOTICE 'orders referencing Commerce: %', rows;
    END IF;
    IF to_regclass('promotions.campaigns') IS NOT NULL THEN
        SELECT count(*) INTO rows FROM promotions.campaigns c
        WHERE EXISTS (SELECT 1 FROM unnest(c."SourceModules") AS module WHERE lower(module) = 'commerce');
        RAISE NOTICE 'promotion campaigns scoped to Commerce: %', rows;
    END IF;
    FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'commerce' ORDER BY tablename LOOP
        EXECUTE format('SELECT count(*) FROM commerce.%I', t.tablename) INTO rows;
        IF t.tablename <> '__EFMigrationsHistory' THEN
            RAISE NOTICE 'commerce.%: % rows', t.tablename, rows;
        END IF;
    END LOOP;
END $$;
ROLLBACK;
