-- 20261003120001-create-table-fin017-attachment.up.sql

-- A file attached to a transaction, an inbox suggestion or a card statement (a boleto, a receipt, an
-- invoice). The bytes live in the module's IFileStorage; storage_backend + storage_key say where, so rows
-- written before an S3 backend keep reading from the database. At most one owner: a file shared with the
-- assistant bot waits with none (the queue) until the user files it; a suggestion's attachments move to
-- its transaction when it is approved.
CREATE TABLE finances.fin017_attachment (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	transaction_id uuid NULL,
	pending_transaction_id uuid NULL,
	card_statement_id uuid NULL,
	kind VARCHAR(20) NOT NULL,
	file_name VARCHAR(255) NOT NULL,
	content_type VARCHAR(255) NOT NULL,
	size_bytes BIGINT NOT NULL,
	storage_backend VARCHAR(50) NOT NULL,
	storage_key VARCHAR(1024) NOT NULL,
	note VARCHAR(500) NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp
);

ALTER TABLE finances.fin017_attachment
ADD CONSTRAINT pk_fin017 PRIMARY KEY (id);

ALTER TABLE finances.fin017_attachment
ADD CONSTRAINT ck_fin017_one_owner
CHECK (num_nonnulls(transaction_id, pending_transaction_id, card_statement_id) <= 1);

ALTER TABLE finances.fin017_attachment
ADD CONSTRAINT ck_fin017_kind
CHECK (kind IN ('bill', 'receipt', 'invoice', 'other'));

ALTER TABLE finances.fin017_attachment
ADD CONSTRAINT fk_fin017_transaction_id FOREIGN KEY (transaction_id)
	REFERENCES finances.fin008_transaction (id);

ALTER TABLE finances.fin017_attachment
ADD CONSTRAINT fk_fin017_pending_transaction_id FOREIGN KEY (pending_transaction_id)
	REFERENCES finances.fin011_pending_transaction (id);

ALTER TABLE finances.fin017_attachment
ADD CONSTRAINT fk_fin017_card_statement_id FOREIGN KEY (card_statement_id)
	REFERENCES finances.fin007_card_statement (id);

CREATE INDEX ix_fin017_transaction_id
ON finances.fin017_attachment (transaction_id);

CREATE INDEX ix_fin017_pending_transaction_id
ON finances.fin017_attachment (pending_transaction_id);

CREATE INDEX ix_fin017_card_statement_id
ON finances.fin017_attachment (card_statement_id);

-- The queue: the user's files with no owner yet.
CREATE INDEX ix_fin017_queued
ON finances.fin017_attachment (user_id)
WHERE transaction_id IS NULL AND pending_transaction_id IS NULL AND card_statement_id IS NULL;
