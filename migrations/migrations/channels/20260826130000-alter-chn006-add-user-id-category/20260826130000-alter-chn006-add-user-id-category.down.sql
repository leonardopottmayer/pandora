-- 20260826130000-alter-chn006-add-user-id-category.down.sql

DROP INDEX IF EXISTS channels.ix_chn006_user_created_at;

ALTER TABLE channels.chn006_notification
DROP COLUMN category;

ALTER TABLE channels.chn006_notification
DROP COLUMN user_id;
