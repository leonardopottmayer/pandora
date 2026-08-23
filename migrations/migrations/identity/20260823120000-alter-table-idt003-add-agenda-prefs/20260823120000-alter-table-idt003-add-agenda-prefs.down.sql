-- 20260823120000-alter-table-idt003-add-agenda-prefs.down.sql

ALTER TABLE identity.idt003_user_preferences
DROP CONSTRAINT IF EXISTS chk_idt003_week_starts_on;

ALTER TABLE identity.idt003_user_preferences
DROP COLUMN IF EXISTS default_alert_offset_minutes;

ALTER TABLE identity.idt003_user_preferences
DROP COLUMN IF EXISTS week_starts_on;

ALTER TABLE identity.idt003_user_preferences
DROP COLUMN IF EXISTS time_zone;
