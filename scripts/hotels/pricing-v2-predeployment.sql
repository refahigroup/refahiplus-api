-- Run after the Hotels pricing-version migration and before releasing pricing v2.
-- The transaction refuses to continue while a legacy Hotel order has reserved funds.

BEGIN;

DO $block$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM orders.orders AS o
        INNER JOIN hotels.hotel_requests AS h ON h.id = o.source_reference_id
        WHERE lower(o.source_module) = 'hotel'
          AND h.pricing_version < 2
          AND o.payment_state = 2
    ) THEN
        RAISE EXCEPTION
            'Legacy Hotel orders with reserved funds exist; release their payment intents through the application before retrying.';
    END IF;
END
$block$;

-- Cancel only unpaid legacy orders. Paid/refunded historical records are untouched.
UPDATE orders.orders AS o
SET status = 6,
    updated_at = CURRENT_TIMESTAMP
FROM hotels.hotel_requests AS h
WHERE h.id = o.source_reference_id
  AND lower(o.source_module) = 'hotel'
  AND h.pricing_version < 2
  AND o.status = 1
  AND o.payment_state = 1;

-- Expire legacy requests that have no order; cancel those whose unpaid order was cancelled.
UPDATE hotels.hotel_requests AS h
SET status = CASE WHEN h.order_id IS NULL THEN 3 ELSE 4 END,
    updated_at = CURRENT_TIMESTAMP,
    expire_at = LEAST(h.expire_at, CURRENT_TIMESTAMP)
WHERE h.pricing_version < 2
  AND h.status IN (1, 2)
  AND (
      h.order_id IS NULL
      OR EXISTS (
          SELECT 1
          FROM orders.orders AS o
          WHERE o.id = h.order_id
            AND o.status = 6
            AND o.payment_state = 1
      )
  );

-- These counts must both be zero before COMMIT.
SELECT count(*) AS legacy_open_hotel_requests
FROM hotels.hotel_requests
WHERE pricing_version < 2
  AND status IN (1, 2);

SELECT count(*) AS legacy_payable_hotel_orders
FROM orders.orders AS o
INNER JOIN hotels.hotel_requests AS h ON h.id = o.source_reference_id
WHERE lower(o.source_module) = 'hotel'
  AND h.pricing_version < 2
  AND o.status = 1
  AND o.payment_state IN (1, 2);

COMMIT;
