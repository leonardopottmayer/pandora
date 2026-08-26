-- 20260826130000-alter-chn006-add-user-id-category.up.sql

-- Delivery history is read per user and filtered by category. The queue row was addressed (channel +
-- recipient) but not attributed, so add both. Nullable: an ad-hoc SendNotificationRequested send has
-- an address but no user, and no category.
ALTER TABLE channels.chn006_notification
ADD COLUMN user_id uuid NULL;

ALTER TABLE channels.chn006_notification
ADD COLUMN category VARCHAR(100) NULL;

-- The history read: a user's notifications, newest first.
CREATE INDEX ix_chn006_user_created_at
ON channels.chn006_notification (user_id, created_at DESC);
