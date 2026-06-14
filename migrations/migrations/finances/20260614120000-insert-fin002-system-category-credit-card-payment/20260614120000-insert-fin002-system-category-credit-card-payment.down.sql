-- 20260614120000-insert-fin002-system-category-credit-card-payment.down.sql

UPDATE finances.fin002_system_category
SET display_order = 7
WHERE code = 'other-financial';

DELETE FROM finances.fin002_system_category WHERE code = 'credit-card-payment';
