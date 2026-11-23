-- 20261123120000-alter-fin001-add-benefit-type.down.sql
-- Drops the wallet column and restores the type list without "benefit". Fails while any account still
-- has type 'benefit': change those accounts to another type first, since the old constraint cannot
-- hold them.

ALTER TABLE finances.fin001_account
DROP CONSTRAINT ck_fin001_benefit_wallet;

ALTER TABLE finances.fin001_account
DROP COLUMN benefit_wallet;

ALTER TABLE finances.fin001_account
DROP CONSTRAINT ck_fin001_type;

ALTER TABLE finances.fin001_account
ADD CONSTRAINT ck_fin001_type
CHECK (type IN ('cash', 'checking', 'savings', 'international', 'crypto', 'investment', 'other'));
