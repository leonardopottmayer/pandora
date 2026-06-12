-- 20260608120000-alter-table-idt003-add-language.up.sql

ALTER TABLE identity.idt003_user_preferences
ADD COLUMN language VARCHAR(10) NOT NULL DEFAULT 'en';

ALTER TABLE identity.idt003_user_preferences
ADD CONSTRAINT chk_idt003_language
CHECK (language IN ('pt-BR', 'en'));
