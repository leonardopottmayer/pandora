-- 20260825120001-create-table-int001-external-account.up.sql

-- One connected third-party account. Holds the credentials Pandora uses on the user's behalf,
-- encrypted at rest with a key that lives outside the database.
CREATE TABLE integrations.int001_external_account (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	provider VARCHAR(40) NOT NULL,
	auth_kind VARCHAR(20) NOT NULL,
	provider_account_id VARCHAR(255) NOT NULL,
	display_name VARCHAR(255) NULL,
	scopes TEXT NOT NULL DEFAULT '',
	access_token_enc TEXT NULL,
	access_token_expires_at TIMESTAMPTZ NULL,
	refresh_token_enc TEXT NULL,
	status VARCHAR(20) NOT NULL,
	connected_at TIMESTAMPTZ NOT NULL,
	last_refreshed_at TIMESTAMPTZ NULL,
	last_error TEXT NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE integrations.int001_external_account
ADD CONSTRAINT pk_int001 PRIMARY KEY (id);

ALTER TABLE integrations.int001_external_account
ADD CONSTRAINT chk_int001_auth_kind
CHECK (auth_kind IN ('oauth', 'api_key'));

ALTER TABLE integrations.int001_external_account
ADD CONSTRAINT chk_int001_status
CHECK (status IN ('connected', 'expired', 'revoked', 'needs_consent'));

-- One account per (user, provider, account). Two Google accounts are already modelled by the
-- discriminating provider_account_id.
CREATE UNIQUE INDEX uq_int001_user_provider_account
ON integrations.int001_external_account (user_id, provider, provider_account_id);
