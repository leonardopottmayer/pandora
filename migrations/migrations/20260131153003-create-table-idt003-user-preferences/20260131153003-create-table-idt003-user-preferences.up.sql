-- 20260131153003-create-table-idt003-user-preferences.up.sql

CREATE TABLE identity.idt003_user_preferences (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	theme VARCHAR(20) NOT NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE identity.idt003_user_preferences
ADD CONSTRAINT pk_idt003 PRIMARY KEY (id);

ALTER TABLE identity.idt003_user_preferences
ADD CONSTRAINT uq_idt003_user_id UNIQUE (user_id);

ALTER TABLE identity.idt003_user_preferences
ADD CONSTRAINT fk_idt003_user_id FOREIGN KEY (user_id) REFERENCES identity.idt001_user (id) ON DELETE CASCADE;

ALTER TABLE identity.idt003_user_preferences
ADD CONSTRAINT fk_idt003_created_by FOREIGN KEY (created_by) REFERENCES identity.idt001_user (id);

ALTER TABLE identity.idt003_user_preferences
ADD CONSTRAINT fk_idt003_updated_by FOREIGN KEY (updated_by) REFERENCES identity.idt001_user (id);

ALTER TABLE identity.idt003_user_preferences
ADD CONSTRAINT chk_idt003_theme
CHECK (theme IN ('light', 'dark', 'system'));
