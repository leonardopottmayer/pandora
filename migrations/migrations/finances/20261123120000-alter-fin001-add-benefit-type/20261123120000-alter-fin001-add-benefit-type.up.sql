-- 20261123120000-alter-fin001-add-benefit-type.up.sql
-- Adds the "benefit" account type (meal/food vouchers such as Flash) and the optional wallet a benefit
-- account holds. The type list lives in a CHECK, so it is dropped and recreated with the new value.
-- Existing rows keep their types and get a NULL wallet, so nothing needs migrating.

ALTER TABLE finances.fin001_account
DROP CONSTRAINT ck_fin001_type;

ALTER TABLE finances.fin001_account
ADD CONSTRAINT ck_fin001_type
CHECK (type IN ('cash', 'checking', 'savings', 'international', 'crypto', 'investment', 'benefit', 'other'));

ALTER TABLE finances.fin001_account
ADD COLUMN benefit_wallet VARCHAR(20) NULL;

-- Only a benefit account says which wallet it is.
ALTER TABLE finances.fin001_account
ADD CONSTRAINT ck_fin001_benefit_wallet
CHECK (benefit_wallet IS NULL OR (type = 'benefit' AND benefit_wallet IN ('food', 'meal', 'flexible')));
