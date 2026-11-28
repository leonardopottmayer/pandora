-- 20261127200000-insert-fin020-savings-asset-classes.down.sql
-- Removes the three classes. Fails while any asset still uses one of them.

DELETE FROM finances.fin020_system_asset_class WHERE code IN ('savings', 'scheduled-plan', 'capital-quotas');

UPDATE finances.fin020_system_asset_class SET display_order = 15 WHERE code = 'other';
