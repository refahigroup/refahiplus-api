using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Wallets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Wallets_AddPaymentPostings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE wallets.payment_intent_postings (
                    posting_id uuid PRIMARY KEY,
                    intent_id uuid NOT NULL REFERENCES wallets.payment_intents(intent_id) ON DELETE CASCADE,
                    wallet_id uuid NOT NULL REFERENCES wallets.wallets(wallet_id),
                    direction smallint NOT NULL,
                    amount_minor bigint NOT NULL CHECK (amount_minor > 0),
                    purpose varchar(80) NOT NULL,
                    sequence integer NOT NULL,
                    CONSTRAINT uq_payment_intent_postings_sequence UNIQUE(intent_id, sequence)
                );
                CREATE INDEX ix_payment_intent_postings_wallet_id
                    ON wallets.payment_intent_postings(wallet_id);

                CREATE TABLE wallets.payment_postings (
                    posting_id uuid PRIMARY KEY,
                    payment_id uuid NOT NULL REFERENCES wallets.payments(payment_id) ON DELETE CASCADE,
                    wallet_id uuid NOT NULL REFERENCES wallets.wallets(wallet_id),
                    direction smallint NOT NULL,
                    amount_minor bigint NOT NULL CHECK (amount_minor > 0),
                    purpose varchar(80) NOT NULL,
                    sequence integer NOT NULL,
                    ledger_entry_id uuid NOT NULL REFERENCES wallets.ledger_entries(ledger_entry_id),
                    CONSTRAINT uq_payment_postings_sequence UNIQUE(payment_id, sequence),
                    CONSTRAINT uq_payment_postings_ledger UNIQUE(ledger_entry_id)
                );
                CREATE INDEX ix_payment_postings_wallet_id ON wallets.payment_postings(wallet_id);
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TABLE IF EXISTS wallets.payment_postings;
                DROP TABLE IF EXISTS wallets.payment_intent_postings;
                """
            );
        }
    }
}
