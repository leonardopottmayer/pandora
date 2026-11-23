-- 20261123130001-insert-fin020-system-asset-classes.down.sql
-- Removes the seeded classes. Fails while any asset still uses one of them.

DELETE FROM finances.fin020_asset_class WHERE user_id IS NULL;
