-- 20260131153008-create-table-idt001-stored-refresh-token.up.sql

CREATE TABLE identity.idt001_stored_refresh_token (
	id uuid NOT NULL,
	key VARCHAR(100) NOT NULL,
	token_hash VARCHAR(64) NOT NULL,
	subject VARCHAR(100) NOT NULL,
	claims_json TEXT NOT NULL,
	expires_at TIMESTAMPTZ NOT NULL,
	metadata_json TEXT,
	consumed_at TIMESTAMPTZ
);

ALTER TABLE identity.idt001_stored_refresh_token
ADD CONSTRAINT pk_idt001_stored_refresh_token PRIMARY KEY (id);

ALTER TABLE identity.idt001_stored_refresh_token
ADD CONSTRAINT uq_idt001_stored_refresh_token_key UNIQUE (key);
