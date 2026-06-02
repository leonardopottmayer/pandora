-- 20260131153006-create-table-usr002-user-preferences.up.sql

CREATE TABLE users.usr002_user_preferences (
	id uuid NOT NULL,
	user_id UUID NOT NULL,
	theme VARCHAR(20) NOT NULL DEFAULT 'light',
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE users.usr002_user_preferences
ADD CONSTRAINT pk_usr002_user_preferences PRIMARY KEY (id);

ALTER TABLE users.usr002_user_preferences
ADD CONSTRAINT uq_usr002_user_preferences_user_id UNIQUE (user_id);

ALTER TABLE users.usr002_user_preferences
ADD CONSTRAINT fk_usr002_user_preferences_user_id FOREIGN KEY (user_id) REFERENCES users.usr001_user (id) ON DELETE CASCADE;

ALTER TABLE users.usr002_user_preferences
ADD CONSTRAINT fk_usr002_user_preferences_created_by FOREIGN KEY (created_by) REFERENCES users.usr001_user (id);

ALTER TABLE users.usr002_user_preferences
ADD CONSTRAINT fk_usr002_user_preferences_updated_by FOREIGN KEY (updated_by) REFERENCES users.usr001_user (id);

ALTER TABLE users.usr002_user_preferences
ADD CONSTRAINT chk_usr002_user_preferences_theme
CHECK (theme IN ('light', 'dark', 'system'));
