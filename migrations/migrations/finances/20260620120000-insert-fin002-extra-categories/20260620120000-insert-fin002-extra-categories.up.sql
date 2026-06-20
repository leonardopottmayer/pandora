-- 20260620120000-insert-fin002-extra-categories.up.sql
-- Additive system categories: a Telecom group (internet / mobile / landline), an Accounting child
-- under Financial Expenses, a Hosting child under Subscriptions, and a Supplements child under Health.
-- Inserts are idempotent via ON CONFLICT (code) DO NOTHING. To keep the Telecom expense parent inside
-- the expense block (which the UI lists together with income, ordered by display_order), the income
-- parents shift one position down so Telecom can take slot 16 right after Misc Expense (15).

UPDATE finances.fin002_system_category
SET display_order = display_order + 1
WHERE parent_category_id IS NULL AND transaction_nature = 'income';

-- Telecom (new top-level expense parent) + children
INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('telecom', 'Telecom', 'expense', NULL, 16, false, true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('internet', 'Internet', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'telecom'), 1, false, true),
	('mobile-phone', 'Mobile Phone', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'telecom'), 2, false, true),
	('landline-phone', 'Landline Phone', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'telecom'), 3, false, true),
	('other-telecom', 'Other Telecom', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'telecom'), 4, true, true)
ON CONFLICT (code) DO NOTHING;

-- Accounting under Financial Expenses (slots before the catch-all, which moves to 9)
UPDATE finances.fin002_system_category SET display_order = 9 WHERE code = 'other-financial';
INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('accounting', 'Accounting', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'financial-expenses'), 8, false, true)
ON CONFLICT (code) DO NOTHING;

-- Hosting under Subscriptions (slots before the catch-all, which moves to 6)
UPDATE finances.fin002_system_category SET display_order = 6 WHERE code = 'other-subscriptions';
INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('hosting', 'Hosting', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'subscriptions'), 5, false, true)
ON CONFLICT (code) DO NOTHING;

-- Supplements under Health (slots before the catch-all, which moves to 8)
UPDATE finances.fin002_system_category SET display_order = 8 WHERE code = 'other-health';
INSERT INTO finances.fin002_system_category (code, name, transaction_nature, parent_category_id, display_order, is_other, is_active) VALUES
	('supplements', 'Supplements', 'expense', (SELECT id FROM finances.fin002_system_category WHERE code = 'health'), 7, false, true)
ON CONFLICT (code) DO NOTHING;
