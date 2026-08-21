-- 20260817130005-create-table-chn003-interaction.up.sql

-- A registered inline button and its route back. The rendered callback_data is this row's id, which
-- is how a 64-byte Telegram callback carries a full (user, module, action, payload) without holding
-- any of it. Single use and short lived: a second tap is "expired", not a second command.
CREATE TABLE channels.chn003_interaction (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	owner_module VARCHAR(50) NOT NULL,
	action VARCHAR(100) NOT NULL,
	-- Opaque owner payload, returned intact. Text, not jsonb: an owner may store a bare id (a
	-- reminder id, say), not necessarily a JSON document.
	payload TEXT NULL,
	notification_id uuid NULL,
	expires_at TIMESTAMPTZ NOT NULL,
	consumed_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE channels.chn003_interaction
ADD CONSTRAINT pk_chn003 PRIMARY KEY (id);

-- The button was declared by a queued notification; null for buttons on system messages.
ALTER TABLE channels.chn003_interaction
ADD CONSTRAINT fk_chn003_notification
FOREIGN KEY (notification_id) REFERENCES channels.chn006_notification (id) ON DELETE SET NULL;
