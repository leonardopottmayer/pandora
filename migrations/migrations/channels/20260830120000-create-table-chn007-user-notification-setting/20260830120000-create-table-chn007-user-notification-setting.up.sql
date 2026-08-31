-- 20260830120000-create-table-chn007-user-notification-setting.up.sql

-- A user's cross-category delivery settings. Today that is quiet hours: one daily "do not disturb"
-- window, expressed in the user's own IANA time zone (resolved from Identity preferences at delivery
-- time -- no zone is stored here), during which notifications are suppressed. Global on purpose: one
-- window, not one per category. Per-category muting already lives on chn005. Security notifications
-- (identity.*) never consult this -- they are mandatory.
--
-- The window is two wall-clock times with no date. quiet_hours_end is exclusive and may be earlier
-- than quiet_hours_start, which denotes a window that wraps past midnight (e.g. 22:00 -> 07:00). All
-- three quiet_hours_* columns are null together when quiet hours are off.
CREATE TABLE channels.chn007_user_notification_setting (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	quiet_hours_start TIME NULL,
	quiet_hours_end TIME NULL,
	quiet_hours_behaviour VARCHAR(20) NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE channels.chn007_user_notification_setting
ADD CONSTRAINT pk_chn007 PRIMARY KEY (id);

-- One settings row per user.
CREATE UNIQUE INDEX uq_chn007_user
ON channels.chn007_user_notification_setting (user_id);
