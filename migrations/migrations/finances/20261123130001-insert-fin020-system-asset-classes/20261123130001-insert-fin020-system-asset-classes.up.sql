-- 20261123130001-insert-fin020-system-asset-classes.up.sql
-- Idempotent seed of the system asset classes. The UI translates them by code. Re-runnable:
-- ON CONFLICT (code) DO NOTHING.

INSERT INTO finances.fin020_system_asset_class (code, name, behavior, display_order) VALUES
	('stock', 'Stock', 'unit', 1),
	('fii', 'Real estate fund (FII)', 'unit', 2),
	('etf', 'ETF', 'unit', 3),
	('bdr', 'BDR', 'unit', 4),
	('fund', 'Investment fund', 'unit', 5),
	('pension', 'Pension plan', 'unit', 6),
	('crypto', 'Crypto', 'unit', 7),
	('currency', 'Currency', 'unit', 8),
	('cdb', 'CDB', 'application', 9),
	('lci', 'LCI', 'application', 10),
	('lca', 'LCA', 'application', 11),
	('treasury', 'Treasury bond', 'application', 12),
	('debenture', 'Debenture', 'application', 13),
	('coe', 'COE', 'application', 14),
	('other', 'Other', 'unit', 15)
ON CONFLICT (code) DO NOTHING;
