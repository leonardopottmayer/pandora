-- 20261127200000-insert-fin020-savings-asset-classes.up.sql
-- Money balances that take contributions over time: a savings account, a cooperative's scheduled plan and
-- its capital quotas. All are applications, so later buys add to the same position. "Other" stays last.

INSERT INTO finances.fin020_system_asset_class (code, name, behavior, display_order) VALUES
	('savings', 'Savings account', 'application', 15),
	('scheduled-plan', 'Scheduled savings plan', 'application', 16),
	('capital-quotas', 'Cooperative capital quotas', 'application', 17)
ON CONFLICT (code) DO NOTHING;

UPDATE finances.fin020_system_asset_class SET display_order = 18 WHERE code = 'other';
