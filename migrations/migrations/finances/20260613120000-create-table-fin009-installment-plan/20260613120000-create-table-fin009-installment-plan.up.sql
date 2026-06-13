-- 20260613120000-create-table-fin009-installment-plan.up.sql

CREATE TABLE finances.fin009_installment_plan (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	card_id uuid NOT NULL,
	origin VARCHAR(10) NOT NULL DEFAULT 'manual',
	total_amount NUMERIC(20,8) NOT NULL,
	total_is_estimate BOOLEAN NOT NULL DEFAULT false,
	installment_count SMALLINT NOT NULL,
	first_reference_month VARCHAR(7) NOT NULL,
	description VARCHAR(255) NOT NULL,
	normalized_description VARCHAR(255) NOT NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin009_installment_plan
ADD CONSTRAINT pk_fin009 PRIMARY KEY (id);

ALTER TABLE finances.fin009_installment_plan
ADD CONSTRAINT ck_fin009_origin
CHECK (origin IN ('manual', 'import'));

ALTER TABLE finances.fin009_installment_plan
ADD CONSTRAINT ck_fin009_installment_count CHECK (installment_count >= 2);

ALTER TABLE finances.fin009_installment_plan
ADD CONSTRAINT ck_fin009_total_amount CHECK (total_amount > 0);

ALTER TABLE finances.fin009_installment_plan
ADD CONSTRAINT ck_fin009_first_reference_month
CHECK (first_reference_month ~ '^[0-9]{4}-[0-9]{2}$');

ALTER TABLE finances.fin009_installment_plan
ADD CONSTRAINT fk_fin009_card_id FOREIGN KEY (card_id)
	REFERENCES finances.fin006_card (id);

CREATE INDEX ix_fin009_user_id
ON finances.fin009_installment_plan (user_id);

CREATE INDEX ix_fin009_card_normalized_description
ON finances.fin009_installment_plan (card_id, normalized_description);
