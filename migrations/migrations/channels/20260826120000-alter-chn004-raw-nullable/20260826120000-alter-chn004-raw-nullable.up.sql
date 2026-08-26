-- 20260826120000-alter-chn004-raw-nullable.up.sql

-- The raw inbound payload is personal data (message text, and eventually voice transcripts) kept only
-- for debugging. The retention job clears it once it ages out by setting it to NULL -- the row itself
-- is never deleted, because it is the idempotency guard and the long-polling offset. So raw must be
-- nullable, where before it was NOT NULL.
ALTER TABLE channels.chn004_inbound_update
ALTER COLUMN raw DROP NOT NULL;

-- Supports the retention scan ("rows received before the cutoff whose raw is still present"). Partial
-- on `raw IS NOT NULL` so it only ever indexes rows still holding a payload -- purged rows drop out.
CREATE INDEX ix_chn004_received_at_unpurged
ON channels.chn004_inbound_update (received_at)
WHERE raw IS NOT NULL;
