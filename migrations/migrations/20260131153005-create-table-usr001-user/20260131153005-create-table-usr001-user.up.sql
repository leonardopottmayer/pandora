-- 20260131153005-create-table-usr001-user.up.sql

CREATE TABLE users.usr001_user (
	id uuid NOT NULL,
	name VARCHAR(150) NOT NULL,
	username VARCHAR(50) NOT NULL,
	email VARCHAR(255) NOT NULL,
	password TEXT NOT NULL,
	status VARCHAR(20) NOT NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE users.usr001_user
ADD CONSTRAINT pk_usr001_user PRIMARY KEY (id);

ALTER TABLE users.usr001_user
ADD CONSTRAINT uq_usr001_user_username UNIQUE (username);

ALTER TABLE users.usr001_user
ADD CONSTRAINT uq_usr001_user_email UNIQUE (email);

ALTER TABLE users.usr001_user
ADD CONSTRAINT fk_usr001_user_created_by FOREIGN KEY (created_by) REFERENCES users.usr001_user (id);

ALTER TABLE users.usr001_user
ADD CONSTRAINT fk_usr001_user_updated_by FOREIGN KEY (updated_by) REFERENCES users.usr001_user (id);

ALTER TABLE users.usr001_user
ADD CONSTRAINT chk_usr001_user_status
CHECK (status IN ('active', 'blocked'));
