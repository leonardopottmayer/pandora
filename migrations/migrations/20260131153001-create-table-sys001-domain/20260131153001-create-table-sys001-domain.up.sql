-- 20260131153001-create-table-sys001-domain.up.sql

CREATE TABLE system.sys001_domain (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	domain_name VARCHAR(100) NOT NULL,
	item_name VARCHAR(100) NOT NULL,
	item_value VARCHAR(100) NOT NULL,
	item_description TEXT NULL
);

ALTER TABLE system.sys001_domain
ADD CONSTRAINT pk_sys001_domain PRIMARY KEY (id);

ALTER TABLE system.sys001_domain
ADD CONSTRAINT uq_sys001_domain_domain_name_item_value UNIQUE (domain_name, item_value);
