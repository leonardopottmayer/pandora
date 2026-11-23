-- 20261122120000-alter-fin001-fin012-add-bank-mercado-pago.down.sql
-- Restores the list without Mercado Pago. Fails while any account or layout still uses 323: unlink
-- them first (set bank_code to NULL), since the old constraint cannot hold them.

ALTER TABLE finances.fin001_account
DROP CONSTRAINT ck_fin001_bank_code;

ALTER TABLE finances.fin001_account
ADD CONSTRAINT ck_fin001_bank_code
CHECK (bank_code IS NULL OR bank_code IN ('077', '085', '260', '341'));

ALTER TABLE finances.fin012_import_layout
DROP CONSTRAINT ck_fin012_bank_code;

ALTER TABLE finances.fin012_import_layout
ADD CONSTRAINT ck_fin012_bank_code
CHECK (bank_code IS NULL OR bank_code IN ('077', '085', '260', '341'));
