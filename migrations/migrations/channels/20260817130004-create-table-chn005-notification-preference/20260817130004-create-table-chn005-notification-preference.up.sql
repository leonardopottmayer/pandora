-- 20260817130004-create-table-chn005-notification-preference.up.sql

-- Delivery policy per category: which channels a given kind of notification goes out on, as an
-- ordered array. An empty array means the user muted that category. Security notifications
-- (identity.*) never consult this -- they are mandatory and take the fact->template path.
--
-- Quiet hours are intentionally absent for now: they need the user's IANA time zone, which the
-- Identity module does not carry yet. They join this table once that lands.
CREATE TABLE channels.chn005_notification_preference (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	category VARCHAR(100) NOT NULL,
	channels TEXT[] NOT NULL DEFAULT '{}',
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE channels.chn005_notification_preference
ADD CONSTRAINT pk_chn005 PRIMARY KEY (id);

-- One preference row per category per user.
CREATE UNIQUE INDEX uq_chn005_user_category
ON channels.chn005_notification_preference (user_id, category);
