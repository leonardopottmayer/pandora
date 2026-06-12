-- 20260612120002-create-table-fin002-system-category.up.sql

CREATE TABLE finances.fin002_system_category (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	code VARCHAR(60) NOT NULL,
	name VARCHAR(100) NOT NULL,
	transaction_nature VARCHAR(10) NOT NULL,
	parent_category_id uuid NULL,
	color VARCHAR(20) NULL,
	icon VARCHAR(50) NULL,
	display_order INT NOT NULL,
	is_other BOOLEAN NOT NULL DEFAULT false,
	is_active BOOLEAN NOT NULL DEFAULT true,
	notes VARCHAR(255) NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin002_system_category
ADD CONSTRAINT pk_fin002 PRIMARY KEY (id);

ALTER TABLE finances.fin002_system_category
ADD CONSTRAINT uq_fin002_code UNIQUE (code);

ALTER TABLE finances.fin002_system_category
ADD CONSTRAINT fk_fin002_parent_category_id FOREIGN KEY (parent_category_id)
	REFERENCES finances.fin002_system_category (id);

ALTER TABLE finances.fin002_system_category
ADD CONSTRAINT ck_fin002_transaction_nature
CHECK (transaction_nature IN ('expense', 'income'));

CREATE INDEX ix_fin002_parent_category_id
ON finances.fin002_system_category (parent_category_id);
