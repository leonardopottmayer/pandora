-- 20260614120000-insert-fin002-system-category-credit-card-payment.up.sql
-- Adds a system category for card statement payments, so users can filter/identify them. Display
-- only — does not affect balances or calculations.

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('credit-card-payment', 'Credit Card Payment', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'financial-expenses'), 7, false, true)
ON CONFLICT (code) DO NOTHING;

UPDATE finances.fin002_system_category
SET display_order = 8
WHERE code = 'other-financial';
