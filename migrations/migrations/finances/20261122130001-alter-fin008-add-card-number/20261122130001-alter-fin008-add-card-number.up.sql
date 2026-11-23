-- 20261122130001-alter-fin008-add-card-number.up.sql
-- Which of the card's numbers a card transaction was made with. Optional: imports don't carry it and
-- existing transactions stay without it.

ALTER TABLE finances.fin008_transaction
ADD COLUMN card_number_id uuid NULL;

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_card_number_id FOREIGN KEY (card_number_id)
	REFERENCES finances.fin019_card_number (id);

CREATE INDEX ix_fin008_card_number_id
ON finances.fin008_transaction (card_number_id)
WHERE card_number_id IS NOT NULL;
