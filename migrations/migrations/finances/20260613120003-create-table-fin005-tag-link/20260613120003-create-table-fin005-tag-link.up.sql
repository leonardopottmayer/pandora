-- 20260613120003-create-table-fin005-tag-link.up.sql

CREATE TABLE finances.fin005_tag_link (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	tag_id uuid NOT NULL,
	entity_type VARCHAR(30) NOT NULL,
	entity_id uuid NOT NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin005_tag_link
ADD CONSTRAINT pk_fin005 PRIMARY KEY (id);

ALTER TABLE finances.fin005_tag_link
ADD CONSTRAINT uq_fin005_tag_entity UNIQUE (tag_id, entity_type, entity_id);

ALTER TABLE finances.fin005_tag_link
ADD CONSTRAINT fk_fin005_tag_id FOREIGN KEY (tag_id)
	REFERENCES finances.fin004_tag (id) ON DELETE CASCADE;

ALTER TABLE finances.fin005_tag_link
ADD CONSTRAINT ck_fin005_entity_type
CHECK (entity_type IN ('account', 'card', 'card-statement', 'transaction', 'recurring-transaction', 'pending-transaction'));

CREATE INDEX ix_fin005_entity
ON finances.fin005_tag_link (entity_type, entity_id);

CREATE INDEX ix_fin005_tag_id
ON finances.fin005_tag_link (tag_id);
