-- 20261122120000-alter-fin001-fin012-add-bank-mercado-pago.up.sql
-- Adds Mercado Pago (COMPE 323) to the supported banks. The list of codes lives in a CHECK on each
-- table, so the constraint is dropped and recreated with the new code. Existing rows only hold codes
-- from the old list, which the new one contains, so nothing needs migrating.

ALTER TABLE finances.fin001_account
DROP CONSTRAINT ck_fin001_bank_code;

ALTER TABLE finances.fin001_account
ADD CONSTRAINT ck_fin001_bank_code
CHECK (bank_code IS NULL OR bank_code IN ('077', '085', '260', '323', '341'));

ALTER TABLE finances.fin012_import_layout
DROP CONSTRAINT ck_fin012_bank_code;

ALTER TABLE finances.fin012_import_layout
ADD CONSTRAINT ck_fin012_bank_code
CHECK (bank_code IS NULL OR bank_code IN ('077', '085', '260', '323', '341'));
