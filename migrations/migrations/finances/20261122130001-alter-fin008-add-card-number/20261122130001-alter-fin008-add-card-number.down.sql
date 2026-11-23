-- 20261122130001-alter-fin008-add-card-number.down.sql

DROP INDEX finances.ix_fin008_card_number_id;

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT fk_fin008_card_number_id;

ALTER TABLE finances.fin008_transaction
DROP COLUMN card_number_id;
