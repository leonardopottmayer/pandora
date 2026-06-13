-- 20260613120001-alter-table-fin008-add-installment-columns.up.sql

ALTER TABLE finances.fin008_transaction
ADD COLUMN installment_plan_id uuid NULL,
ADD COLUMN installment_number SMALLINT NULL;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_installment_number
CHECK (installment_number IS NULL OR installment_number >= 1);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT ck_fin008_installment_pairing
CHECK (
	(installment_plan_id IS NULL AND installment_number IS NULL) OR
	(installment_plan_id IS NOT NULL AND installment_number IS NOT NULL)
);

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_installment_plan_id FOREIGN KEY (installment_plan_id)
	REFERENCES finances.fin009_installment_plan (id);

CREATE INDEX ix_fin008_installment_plan_id
ON finances.fin008_transaction (installment_plan_id);
