using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Wallets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Wallets_AddExplicitLedgerBalanceEffects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "available_delta_minor",
                schema: "wallets",
                table: "ledger_entries",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(
                "alter table wallets.refund_operation_idempotency add column request_hash bytea null;"
            );

            migrationBuilder.AddColumn<long>(
                name: "pending_delta_minor",
                schema: "wallets",
                table: "ledger_entries",
                type: "bigint",
                nullable: true);

            // Payment/Debit is historically overloaded: funding capture affects Pending,
            // while a posting debit affects Available. Refuse any row that cannot be
            // identified from its immutable relationship record.
            migrationBuilder.Sql(
                """
                do $$
                begin
                  if exists (
                    select 1
                    from wallets.ledger_entries le
                    where le.amount_minor is null or le.amount_minor <= 0 or le.currency is null or not (
                      (le.operation_type = 1 and le.entry_type = 1) or
                      (le.operation_type = 2 and le.entry_type = 3) or
                      (le.operation_type = 3 and le.entry_type = 1) or
                      (le.operation_type = 3 and le.entry_type = 2 and
                        ((exists(select 1 from wallets.payment_allocations pa where pa.ledger_entry_id = le.ledger_entry_id))::int +
                         (exists(select 1 from wallets.payment_postings pp where pp.ledger_entry_id = le.ledger_entry_id))::int) = 1) or
                      (le.operation_type = 4 and le.entry_type = 4) or
                      (le.operation_type = 5 and le.entry_type in (1, 2))
                    )
                  ) then
                    raise exception 'Wallet ledger contains an unsupported or ambiguous historical balance effect; migration aborted.';
                  end if;
                end $$;

                update wallets.ledger_entries le
                set available_delta_minor = case
                      when le.operation_type = 1 and le.entry_type = 1 then le.amount_minor
                      when le.operation_type = 2 and le.entry_type = 3 then -le.amount_minor
                      when le.operation_type = 3 and le.entry_type = 1 then le.amount_minor
                      when le.operation_type = 3 and le.entry_type = 2 and exists(
                        select 1 from wallets.payment_postings pp where pp.ledger_entry_id = le.ledger_entry_id
                      ) then -le.amount_minor
                      when le.operation_type = 4 and le.entry_type = 4 then le.amount_minor
                      when le.operation_type = 5 and le.entry_type = 1 then le.amount_minor
                      when le.operation_type = 5 and le.entry_type = 2 then -le.amount_minor
                      else 0
                    end,
                    pending_delta_minor = case
                      when le.operation_type = 2 and le.entry_type = 3 then le.amount_minor
                      when le.operation_type = 3 and le.entry_type = 2 and exists(
                        select 1 from wallets.payment_allocations pa where pa.ledger_entry_id = le.ledger_entry_id
                      ) then -le.amount_minor
                      when le.operation_type = 4 and le.entry_type = 4 then -le.amount_minor
                      else 0
                    end;

                update wallets.ledger_entries release_entry
                set related_entry_id = hold_entry.ledger_entry_id,
                    relation_type = 1
                from wallets.payment_intents intent
                join wallets.ledger_entries hold_entry
                  on hold_entry.external_reference = 'intent:' || intent.intent_id::text
                 and hold_entry.operation_type = 2
                 and hold_entry.entry_type = 3
                where release_entry.operation_type = 4
                  and release_entry.entry_type = 4
                  and release_entry.related_entry_id is null
                  and release_entry.wallet_id = hold_entry.wallet_id
                  and release_entry.external_reference like '%intent:' || intent.intent_id::text;

                do $$
                begin
                  if exists (
                    select 1
                    from wallets.wallet_balances b
                    left join (
                      select wallet_id,
                             sum(available_delta_minor) available_minor,
                             sum(pending_delta_minor) pending_minor
                      from wallets.ledger_entries
                      group by wallet_id
                    ) l on l.wallet_id = b.wallet_id
                    where b.available_minor <> coalesce(l.available_minor, 0)
                       or b.pending_minor <> coalesce(l.pending_minor, 0)
                  ) then
                    raise exception 'Wallet balance projection does not match safely backfilled ledger effects; migration aborted for manual remediation.';
                  end if;
                end $$;

                alter table wallets.ledger_entries
                  alter column available_delta_minor set not null,
                  alter column pending_delta_minor set not null,
                  alter column amount_minor set not null,
                  alter column currency set not null;

                alter table wallets.ledger_entries
                  add constraint ck_ledger_amount_positive check (amount_minor > 0),
                  add constraint ck_ledger_effect_nonzero check (available_delta_minor <> 0 or pending_delta_minor <> 0),
                  add constraint ck_ledger_relation_consistent check (
                    (related_entry_id is null and relation_type = 0)
                    or (related_entry_id is not null and relation_type <> 0)
                  ),
                  add constraint fk_ledger_wallet foreign key (wallet_id) references wallets.wallets(wallet_id) on delete restrict;

                alter table wallets.wallet_balances
                  add constraint ck_wallet_balance_available_nonnegative check (available_minor >= 0),
                  add constraint ck_wallet_balance_pending_nonnegative check (pending_minor >= 0),
                  add constraint fk_wallet_balance_wallet foreign key (wallet_id) references wallets.wallets(wallet_id) on delete cascade;

                alter table wallets.payment_intents
                  add constraint ck_payment_intent_amount_positive check (amount_minor > 0);
                alter table wallets.payment_intent_allocations
                  add constraint ck_payment_intent_allocation_amount_positive check (amount_minor > 0),
                  add constraint ux_payment_intent_allocation_wallet unique (intent_id, wallet_id);
                alter table wallets.payments
                  add constraint ck_payment_amount_positive check (amount_minor > 0);
                alter table wallets.payment_allocations
                  add constraint ck_payment_allocation_amount_positive check (amount_minor > 0),
                  add constraint ux_payment_allocation_wallet unique (payment_id, wallet_id);
                alter table wallets.refunds
                  add constraint ck_refund_amount_positive check (amount_minor > 0);
                alter table wallets.refund_allocations
                  add constraint ck_refund_allocation_amount_positive check (amount_minor > 0),
                  add constraint ux_refund_allocation_wallet unique (refund_id, wallet_id);
                alter table wallets.payment_intent_postings
                  add constraint ck_payment_intent_posting_amount_positive check (amount_minor > 0),
                  add constraint ck_payment_intent_posting_direction check (direction in (1, 2));
                alter table wallets.payment_postings
                  add constraint ck_payment_posting_amount_positive check (amount_minor > 0),
                  add constraint ck_payment_posting_direction check (direction in (1, 2));
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "alter table wallets.refund_operation_idempotency drop column if exists request_hash;"
            );
            migrationBuilder.Sql(
                """
                alter table wallets.wallet_balances
                  drop constraint if exists fk_wallet_balance_wallet,
                  drop constraint if exists ck_wallet_balance_pending_nonnegative,
                  drop constraint if exists ck_wallet_balance_available_nonnegative;
                alter table wallets.ledger_entries
                  drop constraint if exists fk_ledger_wallet,
                  drop constraint if exists ck_ledger_relation_consistent,
                  drop constraint if exists ck_ledger_effect_nonzero,
                  drop constraint if exists ck_ledger_amount_positive;
                alter table wallets.payment_intents drop constraint if exists ck_payment_intent_amount_positive;
                alter table wallets.payment_intent_allocations
                  drop constraint if exists ux_payment_intent_allocation_wallet,
                  drop constraint if exists ck_payment_intent_allocation_amount_positive;
                alter table wallets.payments drop constraint if exists ck_payment_amount_positive;
                alter table wallets.payment_allocations
                  drop constraint if exists ux_payment_allocation_wallet,
                  drop constraint if exists ck_payment_allocation_amount_positive;
                alter table wallets.refunds drop constraint if exists ck_refund_amount_positive;
                alter table wallets.refund_allocations
                  drop constraint if exists ux_refund_allocation_wallet,
                  drop constraint if exists ck_refund_allocation_amount_positive;
                alter table wallets.payment_intent_postings
                  drop constraint if exists ck_payment_intent_posting_direction,
                  drop constraint if exists ck_payment_intent_posting_amount_positive;
                alter table wallets.payment_postings
                  drop constraint if exists ck_payment_posting_direction,
                  drop constraint if exists ck_payment_posting_amount_positive;
                """
            );
            migrationBuilder.DropColumn(
                name: "available_delta_minor",
                schema: "wallets",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "pending_delta_minor",
                schema: "wallets",
                table: "ledger_entries");
        }
    }
}
