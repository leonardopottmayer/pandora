-- 20260616120002-alter-table-fin008-add-recurrence-refs.up.sql

ALTER TABLE finances.fin008_transaction
ADD COLUMN pending_transaction_id uuid NULL;

ALTER TABLE finances.fin008_transaction
ADD COLUMN recurring_transaction_id uuid NULL;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_pending_transaction_id FOREIGN KEY (pending_transaction_id)
	REFERENCES finances.fin011_pending_transaction (id);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_recurring_transaction_id FOREIGN KEY (recurring_transaction_id)
	REFERENCES finances.fin010_recurring_transaction (id);

CREATE INDEX ix_fin008_pending_transaction_id
ON finances.fin008_transaction (pending_transaction_id);

CREATE INDEX ix_fin008_recurring_transaction_id
ON finances.fin008_transaction (recurring_transaction_id);
