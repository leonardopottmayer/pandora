-- 20260817130000-create-table-chn001-user-channel.up.sql

-- Where a user can be reached, per channel. The join behind every delivery decision: an address is
-- only usable when it is both verified and enabled.
CREATE TABLE channels.chn001_user_channel (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	channel VARCHAR(20) NOT NULL,
	address VARCHAR(255) NOT NULL,
	locale VARCHAR(10) NOT NULL,
	is_verified BOOLEAN NOT NULL DEFAULT false,
	verified_at TIMESTAMPTZ NULL,
	is_enabled BOOLEAN NOT NULL DEFAULT true,
	disabled_reason TEXT NULL,
	metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE channels.chn001_user_channel
ADD CONSTRAINT pk_chn001 PRIMARY KEY (id);

ALTER TABLE channels.chn001_user_channel
ADD CONSTRAINT chk_chn001_channel
CHECK (channel IN ('email', 'telegram'));

-- One address per channel per user. Two Telegram chats for one person is a deliberate future change.
CREATE UNIQUE INDEX uq_chn001_user_channel
ON channels.chn001_user_channel (user_id, channel);

-- An address belongs to one account: without this, a chat id could be linked twice and inbound
-- updates would resolve to whichever row came first.
CREATE UNIQUE INDEX uq_chn001_channel_address
ON channels.chn001_user_channel (channel, address);
