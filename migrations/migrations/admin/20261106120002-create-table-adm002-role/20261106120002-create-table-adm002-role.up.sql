-- 20261106120002-create-table-adm002-role.up.sql

-- A named set of permission patterns. System roles (admin) cannot be edited or deleted; the default
-- role is given to every account when it is activated.
CREATE TABLE admin.adm002_role (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	name VARCHAR(100) NOT NULL,
	description VARCHAR(500) NULL,
	is_system BOOLEAN NOT NULL DEFAULT false,
	is_default BOOLEAN NOT NULL DEFAULT false,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE admin.adm002_role
ADD CONSTRAINT pk_adm002 PRIMARY KEY (id);

ALTER TABLE admin.adm002_role
ADD CONSTRAINT uq_adm002_name UNIQUE (name);

-- At most one default role.
CREATE UNIQUE INDEX uq_adm002_is_default ON admin.adm002_role (is_default) WHERE is_default;
