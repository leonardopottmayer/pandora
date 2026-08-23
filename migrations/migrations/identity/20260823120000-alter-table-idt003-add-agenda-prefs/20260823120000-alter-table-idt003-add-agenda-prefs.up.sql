-- 20260823120000-alter-table-idt003-add-agenda-prefs.up.sql

ALTER TABLE identity.idt003_user_preferences
ADD COLUMN time_zone VARCHAR(64) NOT NULL DEFAULT 'America/Sao_Paulo';

ALTER TABLE identity.idt003_user_preferences
ADD COLUMN week_starts_on VARCHAR(10) NOT NULL DEFAULT 'sunday';

ALTER TABLE identity.idt003_user_preferences
ADD COLUMN default_alert_offset_minutes INTEGER NOT NULL DEFAULT -15;

ALTER TABLE identity.idt003_user_preferences
ADD CONSTRAINT chk_idt003_week_starts_on
CHECK (week_starts_on IN ('sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'));
