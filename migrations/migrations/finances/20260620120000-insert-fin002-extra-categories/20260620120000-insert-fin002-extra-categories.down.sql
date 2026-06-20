-- 20260620120000-insert-fin002-extra-categories.down.sql

-- Remove children first (self-referencing FK), then the Telecom parent.
DELETE FROM finances.fin002_system_category
WHERE code IN ('internet', 'mobile-phone', 'landline-phone', 'other-telecom', 'accounting', 'hosting', 'supplements');
DELETE FROM finances.fin002_system_category WHERE code = 'telecom';

-- Restore the catch-all positions.
UPDATE finances.fin002_system_category SET display_order = 8 WHERE code = 'other-financial';
UPDATE finances.fin002_system_category SET display_order = 5 WHERE code = 'other-subscriptions';
UPDATE finances.fin002_system_category SET display_order = 7 WHERE code = 'other-health';

-- Restore income parent positions.
UPDATE finances.fin002_system_category
SET display_order = display_order - 1
WHERE parent_category_id IS NULL AND transaction_nature = 'income';
