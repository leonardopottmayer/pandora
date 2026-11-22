-- 20261106120005-create-table-adm005-user-permission.up.sql

-- A per-user exception on one pattern. A deny beats every allow and every role.
CREATE TABLE admin.adm005_user_permission (
	user_id uuid NOT NULL,
	pattern VARCHAR(150) NOT NULL,
	effect VARCHAR(10) NOT NULL
);

ALTER TABLE admin.adm005_user_permission
ADD CONSTRAINT pk_adm005 PRIMARY KEY (user_id, pattern);

ALTER TABLE admin.adm005_user_permission
ADD CONSTRAINT chk_adm005_effect
CHECK (effect IN ('allow', 'deny'));
