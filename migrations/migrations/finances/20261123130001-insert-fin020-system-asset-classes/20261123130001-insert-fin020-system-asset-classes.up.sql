-- 20261123130001-insert-fin020-system-asset-classes.up.sql
-- Idempotent seed of the system asset classes. The UI translates them by code. Re-runnable.

INSERT INTO finances.fin020_asset_class (code, name, behavior) VALUES
	('stock', 'Stock', 'unit'),
	('fii', 'Real estate fund (FII)', 'unit'),
	('etf', 'ETF', 'unit'),
	('bdr', 'BDR', 'unit'),
	('fund', 'Investment fund', 'unit'),
	('pension', 'Pension plan', 'unit'),
	('crypto', 'Crypto', 'unit'),
	('currency', 'Currency', 'unit'),
	('cdb', 'CDB', 'application'),
	('lci', 'LCI', 'application'),
	('lca', 'LCA', 'application'),
	('treasury', 'Treasury bond', 'application'),
	('debenture', 'Debenture', 'application'),
	('coe', 'COE', 'application'),
	('other', 'Other', 'unit')
ON CONFLICT (code) WHERE code IS NOT NULL DO NOTHING;
