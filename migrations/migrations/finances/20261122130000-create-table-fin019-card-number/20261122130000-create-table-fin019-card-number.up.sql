-- 20261122130000-create-table-fin019-card-number.up.sql
-- The numbers a card has: the physical card and any virtual ones. They all bill on the card's single
-- statement, so they are children of fin006, not cards of their own. A number used by a transaction
-- is archived instead of deleted, so the history keeps saying which one was used.

CREATE TABLE finances.fin019_card_number (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	card_id uuid NOT NULL,
	kind VARCHAR(20) NOT NULL,
	last_four VARCHAR(4) NOT NULL,
	nickname VARCHAR(50) NULL,
	archived_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin019_card_number
ADD CONSTRAINT pk_fin019 PRIMARY KEY (id);

ALTER TABLE finances.fin019_card_number
ADD CONSTRAINT fk_fin019_card_id FOREIGN KEY (card_id) REFERENCES finances.fin006_card (id);

ALTER TABLE finances.fin019_card_number
ADD CONSTRAINT ck_fin019_kind CHECK (kind IN ('physical', 'virtual'));

ALTER TABLE finances.fin019_card_number
ADD CONSTRAINT ck_fin019_last_four CHECK (last_four ~ '^[0-9]{4}$');

ALTER TABLE finances.fin019_card_number
ADD CONSTRAINT uq_fin019_card_last_four UNIQUE (card_id, last_four);

-- The card's last four digits so far become its physical number. fin006.last_four is no longer read
-- or written by the app; it stays until a later release drops it, so a rollback still finds it.
INSERT INTO finances.fin019_card_number (user_id, card_id, kind, last_four, created_at)
SELECT user_id, id, 'physical', last_four, current_timestamp
FROM finances.fin006_card
WHERE last_four ~ '^[0-9]{4}$';
