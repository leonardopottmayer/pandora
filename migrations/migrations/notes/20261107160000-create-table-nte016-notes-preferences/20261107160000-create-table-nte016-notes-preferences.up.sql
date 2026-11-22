CREATE TABLE notes.nte016_notes_preferences (
    user_id uuid NOT NULL,
    daily_template_id uuid NULL,
    daily_folder_id uuid NULL,
    created_by uuid NULL,
    created_at timestamptz NOT NULL DEFAULT current_timestamp,
    updated_by uuid NULL,
    updated_at timestamptz NULL,
    CONSTRAINT pk_nte016 PRIMARY KEY (user_id)
);
-- References are owner-checked in the application; missing templates/folders fall back on use.
