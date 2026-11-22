-- 20260817130001-create-table-chn002-channel-link-token.up.sql

-- The handshake that ties a chat to an account. Single use, short lived: it is the only thing that
-- authorizes a chat id, which is never accepted from the client.
CREATE TABLE channels.chn002_channel_link_token (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	channel VARCHAR(20) NOT NULL,
	token VARCHAR(64) NOT NULL,
	locale VARCHAR(10) NOT NULL,
	expires_at TIMESTAMPTZ NOT NULL,
	consumed_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE channels.chn002_channel_link_token
ADD CONSTRAINT pk_chn002 PRIMARY KEY (id);

ALTER TABLE channels.chn002_channel_link_token
ADD CONSTRAINT chk_chn002_channel
CHECK (channel IN ('email', 'telegram'));

CREATE UNIQUE INDEX uq_chn002_token
ON channels.chn002_channel_link_token (token);
