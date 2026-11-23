-- 20261123130004-create-table-fin023-position-movement.up.sql
-- Everything that happens to a position, with date, quantity, prices, costs and tax withheld, so returns
-- and taxes can be computed later. The *_before columns keep the position as it was, so the latest
-- movement can be undone. transaction_id is the ledger entry the movement created on the account.

CREATE TABLE finances.fin023_position_movement (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	position_id uuid NOT NULL,
	kind VARCHAR(20) NOT NULL,
	occurred_on DATE NOT NULL,
	quantity NUMERIC(28, 10) NULL,
	unit_price NUMERIC(28, 10) NULL,
	gross_amount NUMERIC(18, 2) NULL,
	costs NUMERIC(18, 2) NOT NULL DEFAULT 0,
	tax_withheld NUMERIC(18, 2) NOT NULL DEFAULT 0,
	net_amount NUMERIC(18, 2) NULL,
	income_kind VARCHAR(30) NULL,
	split_factor NUMERIC(18, 8) NULL,
	realized_result NUMERIC(18, 2) NULL,
	quantity_before NUMERIC(28, 10) NULL,
	average_cost_before NUMERIC(28, 10) NULL,
	invested_amount_before NUMERIC(18, 2) NULL,
	transaction_id uuid NULL,
	notes VARCHAR(255) NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin023_position_movement
ADD CONSTRAINT pk_fin023 PRIMARY KEY (id);

ALTER TABLE finances.fin023_position_movement
ADD CONSTRAINT fk_fin023_position_id FOREIGN KEY (position_id) REFERENCES finances.fin022_position (id);

ALTER TABLE finances.fin023_position_movement
ADD CONSTRAINT fk_fin023_transaction_id FOREIGN KEY (transaction_id) REFERENCES finances.fin008_transaction (id);

ALTER TABLE finances.fin023_position_movement
ADD CONSTRAINT uq_fin023_transaction_id UNIQUE (transaction_id);

ALTER TABLE finances.fin023_position_movement
ADD CONSTRAINT ck_fin023_kind
CHECK (kind IN ('opening', 'buy', 'sell', 'redemption', 'maturity', 'income', 'split', 'come-cotas'));

ALTER TABLE finances.fin023_position_movement
ADD CONSTRAINT ck_fin023_income_kind
CHECK (income_kind IS NULL OR income_kind IN ('dividend', 'interest-on-equity', 'rent', 'coupon', 'other'));

ALTER TABLE finances.fin023_position_movement
ADD CONSTRAINT ck_fin023_amounts CHECK (costs >= 0 AND tax_withheld >= 0);

CREATE INDEX ix_fin023_position_id
ON finances.fin023_position_movement (position_id, created_at);
