-- 20260826120000-alter-chn004-raw-nullable.down.sql

DROP INDEX IF EXISTS channels.ix_chn004_received_at_unpurged;

-- Restoring NOT NULL requires no NULLs present: backfill any already-purged rows with an empty object
-- before reinstating the constraint.
UPDATE channels.chn004_inbound_update
SET raw = '{}'::jsonb
WHERE raw IS NULL;

ALTER TABLE channels.chn004_inbound_update
ALTER COLUMN raw SET NOT NULL;
