-- 20260614120001-alter-table-fin008-add-reversal-columns.up.sql

ALTER TABLE finances.fin008_transaction
ADD COLUMN reversed_transaction_id uuid NULL;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_reversed_transaction_id FOREIGN KEY (reversed_transaction_id)
	REFERENCES finances.fin008_transaction (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT uq_fin008_reversed_transaction_id UNIQUE (reversed_transaction_id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_reversed_transaction_not_self
CHECK (reversed_transaction_id IS NULL OR reversed_transaction_id <> id);

CREATE INDEX ix_fin008_reversed_transaction_id
ON finances.fin008_transaction (reversed_transaction_id);

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT ck_fin008_origin;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_origin
CHECK (origin IN ('manual', 'import', 'recurrence', 'projection', 'reversal'));
