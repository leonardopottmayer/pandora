-- 20261106120003-create-table-adm003-role-permission.up.sql

-- A permission pattern held by a role: an exact key, a prefix wildcard (finances.*) or *.
CREATE TABLE admin.adm003_role_permission (
	role_id uuid NOT NULL,
	pattern VARCHAR(150) NOT NULL
);

ALTER TABLE admin.adm003_role_permission
ADD CONSTRAINT pk_adm003 PRIMARY KEY (role_id, pattern);

ALTER TABLE admin.adm003_role_permission
ADD CONSTRAINT fk_adm003_role_id FOREIGN KEY (role_id) REFERENCES admin.adm002_role (id) ON DELETE CASCADE;
