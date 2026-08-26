-- 20260817130002-create-table-chn004-inbound-update.up.sql

-- Every update the bot received, recorded before it is processed. The provider's own update id makes
-- reprocessing harmless: the long-polling offset is confirmed by writing this row, so a crash between
-- write and processing replays instead of losing the update.
CREATE TABLE channels.chn004_inbound_update (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	provider VARCHAR(20) NOT NULL,
	provider_update_id BIGINT NOT NULL,
	-- Nullable: the retention job clears the raw payload to null once it ages out (personal data kept
	-- only for debugging), while keeping the row -- it is the idempotency guard and long-polling offset.
	raw JSONB NULL,
	user_id uuid NULL,
	classification VARCHAR(20) NOT NULL,
	received_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	processed_at TIMESTAMPTZ NULL
);

ALTER TABLE channels.chn004_inbound_update
ADD CONSTRAINT pk_chn004 PRIMARY KEY (id);

ALTER TABLE channels.chn004_inbound_update
ADD CONSTRAINT chk_chn004_classification
CHECK (classification IN ('Interaction', 'Command', 'Message', 'Discarded'));

-- The idempotency guard. The plan called for this pair as the primary key; it is a unique index
-- instead so the table keeps the uuid_generate_v7() surrogate every other table here has.
CREATE UNIQUE INDEX uq_chn004_provider_update
ON channels.chn004_inbound_update (provider, provider_update_id);

-- "What is the highest update we have seen?" -- the long-polling offset on startup.
CREATE INDEX ix_chn004_provider_update_id_desc
ON channels.chn004_inbound_update (provider, provider_update_id DESC);

-- Supports the retention scan ("rows received before the cutoff whose raw is still present"). Partial
-- on `raw IS NOT NULL` so it only ever indexes rows still holding a payload -- purged rows drop out.
CREATE INDEX ix_chn004_received_at_unpurged
ON channels.chn004_inbound_update (received_at)
WHERE raw IS NOT NULL;
