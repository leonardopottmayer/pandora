-- 20260612120004-create-table-fin003-user-category.up.sql

CREATE TABLE finances.fin003_user_category (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	name VARCHAR(100) NOT NULL,
	transaction_nature VARCHAR(10) NOT NULL,
	parent_category_id uuid NULL,
	color VARCHAR(20) NULL,
	icon VARCHAR(50) NULL,
	display_order INT NOT NULL DEFAULT 0,
	is_active BOOLEAN NOT NULL DEFAULT true,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin003_user_category
ADD CONSTRAINT pk_fin003 PRIMARY KEY (id);

ALTER TABLE finances.fin003_user_category
ADD CONSTRAINT uq_fin003_user_name_parent UNIQUE (user_id, name, parent_category_id);

ALTER TABLE finances.fin003_user_category
ADD CONSTRAINT fk_fin003_parent_category_id FOREIGN KEY (parent_category_id)
	REFERENCES finances.fin003_user_category (id);

ALTER TABLE finances.fin003_user_category
ADD CONSTRAINT ck_fin003_transaction_nature
CHECK (transaction_nature IN ('expense', 'income'));

CREATE INDEX ix_fin003_user_id
ON finances.fin003_user_category (user_id);

CREATE INDEX ix_fin003_parent_category_id
ON finances.fin003_user_category (parent_category_id);
