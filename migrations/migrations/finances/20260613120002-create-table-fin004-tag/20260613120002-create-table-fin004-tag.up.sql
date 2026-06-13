-- 20260613120002-create-table-fin004-tag.up.sql

CREATE TABLE finances.fin004_tag (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	name VARCHAR(50) NOT NULL,
	color VARCHAR(20) NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin004_tag
ADD CONSTRAINT pk_fin004 PRIMARY KEY (id);

ALTER TABLE finances.fin004_tag
ADD CONSTRAINT uq_fin004_user_name UNIQUE (user_id, name);

CREATE INDEX ix_fin004_user_id
ON finances.fin004_tag (user_id);
