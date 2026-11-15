using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.Pools.Infrastructure.Persistence;

namespace Refahi.Modules.Pools.Infrastructure.Migrations;

[DbContext(typeof(PoolsDbContext))]
[Migration("20261114211900_RetireEmptyCommerce")]
public sealed class RetireEmptyCommerce : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        DECLARE table_name text; contains_rows boolean;
        BEGIN
            IF to_regclass('orders.orders') IS NOT NULL THEN
                LOCK TABLE orders.orders IN SHARE MODE;
                IF EXISTS (SELECT 1 FROM orders.orders
                           WHERE lower(source_module) = 'commerce' OR lower(reference_type) = 'commerceorder') THEN
                    RAISE EXCEPTION 'Commerce retirement stopped: Orders still references Commerce';
                END IF;
            END IF;

            IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'commerce') THEN RETURN; END IF;

            FOREACH table_name IN ARRAY ARRAY[
              'provider_operation_attempts', 'provider_tickets', 'provider_fulfillments',
              'order_items', 'orders', 'cart_items', 'carts', 'checkout_sessions',
              'catalog_snapshots', 'provider_receipts']
            LOOP
                IF to_regclass(format('commerce.%I', table_name)) IS NULL THEN CONTINUE; END IF;
                EXECUTE format('LOCK TABLE commerce.%I IN ACCESS EXCLUSIVE MODE', table_name);
                EXECUTE format('SELECT EXISTS (SELECT 1 FROM commerce.%I)', table_name) INTO contains_rows;
                IF contains_rows THEN
                    RAISE EXCEPTION 'Commerce retirement stopped: commerce.% is not empty', table_name;
                END IF;
            END LOOP;

            DROP TABLE IF EXISTS commerce.provider_operation_attempts, commerce.provider_tickets,
                commerce.provider_fulfillments, commerce.order_items, commerce.orders, commerce.cart_items, commerce.carts,
                commerce.checkout_sessions, commerce.catalog_snapshots, commerce.provider_receipts,
                commerce."__EFMigrationsHistory";
            DROP SCHEMA commerce RESTRICT;
        END $$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
        => throw new System.NotSupportedException("Commerce retirement requires restoring its archived migration lineage.");
}
