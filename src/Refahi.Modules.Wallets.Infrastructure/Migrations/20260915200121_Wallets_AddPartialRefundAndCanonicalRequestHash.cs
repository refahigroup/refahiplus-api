using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Wallets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Wallets_AddPartialRefundAndCanonicalRequestHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                alter table wallets.payment_intents
                  add column request_hash bytea null,
                  add column request_hash_version smallint null;

                alter table wallets.refund_operation_idempotency
                  add column request_hash_version smallint null;

                -- Pass 1 hashes used an unversioned delimiter format. They cannot be
                -- truthfully labelled v1, so completed historical rows fall back to
                -- stored refund equivalence after the hash is cleared.
                update wallets.refund_operation_idempotency
                set request_hash = null
                where request_hash is not null;

                drop index wallets.ux_refunds_payment;
                create index ix_refunds_payment on wallets.refunds(payment_id);

                alter table wallets.payment_intents
                  add constraint ck_payment_intent_request_hash_pair check (
                    (request_hash is null and request_hash_version is null)
                    or (request_hash is not null and request_hash_version is not null and request_hash_version > 0)
                  );
                alter table wallets.refund_operation_idempotency
                  add constraint ck_refund_idempotency_request_hash_pair check (
                    (request_hash is null and request_hash_version is null)
                    or (request_hash is not null and request_hash_version is not null and request_hash_version > 0)
                  );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                do $$
                begin
                  if exists (
                    select 1 from wallets.refunds group by payment_id having count(*) > 1
                  ) then
                    raise exception 'Cannot roll back partial-refund schema: one or more payments have multiple refunds.';
                  end if;
                end $$;

                -- The Pass 1 runtime understands null hashes through its stored
                -- result compatibility path, but cannot interpret v1 hashes.
                update wallets.refund_operation_idempotency set request_hash = null;

                alter table wallets.refund_operation_idempotency
                  drop constraint ck_refund_idempotency_request_hash_pair,
                  drop column request_hash_version;
                alter table wallets.payment_intents
                  drop constraint ck_payment_intent_request_hash_pair,
                  drop column request_hash,
                  drop column request_hash_version;

                drop index wallets.ix_refunds_payment;
                create unique index ux_refunds_payment on wallets.refunds(payment_id);
                """
            );
        }
    }
}
