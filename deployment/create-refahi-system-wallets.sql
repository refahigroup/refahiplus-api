-- Creates the Refahi platform revenue and VAT wallets.
--
-- Wallet storage contracts:
--   wallet_type = 1 -> System
--   status      = 1 -> Active
--   currency        -> IRR
--
-- Keep these IDs synchronized with StorePaymentDistribution in application
-- configuration. Change the three UUID constants before the first execution
-- only if another ID namespace is required by the target environment.
--
-- "StorePaymentDistribution": {
--   "RefahiRevenueWalletId": "7525031a-748a-498b-8538-ad9f1625d5e4",
--   "RefahiVatWalletId": "96014c03-bbdb-4a64-a13b-5df37e643c13",
--   "VatRatePercent": 10
-- }

BEGIN;

DO $create_refahi_system_wallets$
DECLARE
    platform_owner_id       constant uuid := '6770ad09-d26f-434d-a4d6-15cc3662e332';
    revenue_wallet_id       constant uuid := '7525031a-748a-498b-8538-ad9f1625d5e4';
    vat_wallet_id           constant uuid := '96014c03-bbdb-4a64-a13b-5df37e643c13';
    system_wallet_type      constant smallint := 1;
    active_wallet_status    constant smallint := 1;
    platform_currency       constant varchar(3) := 'IRR';
    operation_time          constant timestamptz := transaction_timestamp();
    invalid_wallet_count    integer;
    invalid_balance_count   integer;
BEGIN
    IF to_regclass('wallets.wallets') IS NULL
       OR to_regclass('wallets.wallet_balances') IS NULL THEN
        RAISE EXCEPTION
            'Wallet migrations must be applied before creating the Refahi system wallets.';
    END IF;

    IF revenue_wallet_id = vat_wallet_id THEN
        RAISE EXCEPTION 'Revenue and VAT wallet IDs must be different.';
    END IF;

    INSERT INTO wallets.wallets
        (wallet_id, "OwnerId", wallet_type, status, currency, created_at,
         allowed_category_code, contract_expires_at)
    VALUES
        (revenue_wallet_id, platform_owner_id, system_wallet_type,
         active_wallet_status, platform_currency, operation_time, NULL, NULL),
        (vat_wallet_id, platform_owner_id, system_wallet_type,
         active_wallet_status, platform_currency, operation_time, NULL, NULL)
    ON CONFLICT (wallet_id) DO NOTHING;

    SELECT count(*)
      INTO invalid_wallet_count
      FROM (VALUES (revenue_wallet_id), (vat_wallet_id)) AS expected(wallet_id)
      LEFT JOIN wallets.wallets AS wallet
        ON wallet.wallet_id = expected.wallet_id
     WHERE wallet.wallet_id IS NULL
        OR wallet."OwnerId" <> platform_owner_id
        OR wallet.wallet_type <> system_wallet_type
        OR wallet.status <> active_wallet_status
        OR wallet.currency <> platform_currency
        OR wallet.allowed_category_code IS NOT NULL
        OR wallet.contract_expires_at IS NOT NULL;

    IF invalid_wallet_count <> 0 THEN
        RAISE EXCEPTION
            'One or more configured wallet IDs already exist with incompatible attributes.';
    END IF;

    -- payment posting updates wallet_balances directly, so every destination
    -- wallet needs an initialized zero-balance projection.
    INSERT INTO wallets.wallet_balances
        (wallet_id, available_minor, pending_minor, currency,
         last_ledger_entry_id, version, updated_at)
    VALUES
        (revenue_wallet_id, 0, 0, platform_currency, NULL, 1, operation_time),
        (vat_wallet_id, 0, 0, platform_currency, NULL, 1, operation_time)
    ON CONFLICT (wallet_id) DO NOTHING;

    SELECT count(*)
      INTO invalid_balance_count
      FROM (VALUES (revenue_wallet_id), (vat_wallet_id)) AS expected(wallet_id)
      LEFT JOIN wallets.wallet_balances AS balance
        ON balance.wallet_id = expected.wallet_id
     WHERE balance.wallet_id IS NULL
        OR balance.currency <> platform_currency;

    IF invalid_balance_count <> 0 THEN
        RAISE EXCEPTION
            'One or more system wallet balance projections are missing or use an incompatible currency.';
    END IF;
END
$create_refahi_system_wallets$;

COMMIT;

-- Copy these values to the StorePaymentDistribution configuration section.
SELECT
    '7525031a-748a-498b-8538-ad9f1625d5e4'::uuid AS "RefahiRevenueWalletId",
    '96014c03-bbdb-4a64-a13b-5df37e643c13'::uuid AS "RefahiVatWalletId";
