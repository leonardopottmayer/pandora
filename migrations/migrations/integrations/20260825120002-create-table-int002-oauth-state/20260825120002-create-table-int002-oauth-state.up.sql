-- 20260825120002-create-table-int002-oauth-state.up.sql

-- One in-flight authorization request. The callback authenticates by consuming exactly the state it
-- issued: single use, short lived. The PKCE verifier is encrypted for the duration of the flow.
CREATE TABLE integrations.int002_oauth_state (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	provider VARCHAR(40) NOT NULL,
	state VARCHAR(255) NOT NULL,
	code_verifier_enc TEXT NOT NULL,
	redirect_after VARCHAR(500) NOT NULL,
	expires_at TIMESTAMPTZ NOT NULL,
	consumed_at TIMESTAMPTZ NULL
);

ALTER TABLE integrations.int002_oauth_state
ADD CONSTRAINT pk_int002 PRIMARY KEY (id);

-- The state is the CSRF token: unique so a callback resolves to exactly one request.
CREATE UNIQUE INDEX uq_int002_state
ON integrations.int002_oauth_state (state);
