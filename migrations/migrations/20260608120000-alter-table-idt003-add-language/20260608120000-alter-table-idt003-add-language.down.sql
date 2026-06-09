-- 20260608120000-alter-table-idt003-add-language.down.sql

ALTER TABLE identity.idt003_user_preferences
DROP CONSTRAINT IF EXISTS chk_idt003_language;

ALTER TABLE identity.idt003_user_preferences
DROP COLUMN IF EXISTS language;
