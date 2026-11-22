-- 20261006120000-create-table-idt009-device.up.sql

-- A paired client (Pandora Desktop, a headless agent, a phone) that calls the API with its own key
-- instead of the user's session. Only the SHA-256 of the key is stored; the key is shown once.
CREATE TABLE identity.idt009_device (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	name VARCHAR(100) NOT NULL,
	platform VARCHAR(20) NOT NULL,
	form VARCHAR(20) NOT NULL,
	key_hash VARCHAR(64) NOT NULL,
	scopes TEXT[] NOT NULL DEFAULT '{}',
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	last_seen_at TIMESTAMPTZ NULL,
	revoked_at TIMESTAMPTZ NULL
);

ALTER TABLE identity.idt009_device
ADD CONSTRAINT pk_idt009 PRIMARY KEY (id);

ALTER TABLE identity.idt009_device
ADD CONSTRAINT uq_idt009_key_hash UNIQUE (key_hash);

ALTER TABLE identity.idt009_device
ADD CONSTRAINT fk_idt009_user_id FOREIGN KEY (user_id)
REFERENCES identity.idt001_user (id) ON DELETE CASCADE;

ALTER TABLE identity.idt009_device
ADD CONSTRAINT chk_idt009_platform
CHECK (platform IN ('windows', 'linux', 'macos', 'android', 'ios'));

ALTER TABLE identity.idt009_device
ADD CONSTRAINT chk_idt009_form
CHECK (form IN ('desktop', 'headless', 'mobile'));

CREATE INDEX ix_idt009_user_id ON identity.idt009_device (user_id);
