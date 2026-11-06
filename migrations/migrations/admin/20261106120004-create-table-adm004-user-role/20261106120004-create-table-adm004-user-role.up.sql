-- 20261106120004-create-table-adm004-user-role.up.sql

-- user_id references identity.idt001_user only logically: no FK leaves the admin schema.
CREATE TABLE admin.adm004_user_role (
	user_id uuid NOT NULL,
	role_id uuid NOT NULL
);

ALTER TABLE admin.adm004_user_role
ADD CONSTRAINT pk_adm004 PRIMARY KEY (user_id, role_id);

ALTER TABLE admin.adm004_user_role
ADD CONSTRAINT fk_adm004_role_id FOREIGN KEY (role_id) REFERENCES admin.adm002_role (id) ON DELETE CASCADE;

CREATE INDEX ix_adm004_role_id ON admin.adm004_user_role (role_id);
