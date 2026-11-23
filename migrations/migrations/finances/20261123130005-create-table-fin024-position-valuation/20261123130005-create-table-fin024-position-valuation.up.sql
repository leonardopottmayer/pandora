-- 20261123130005-create-table-fin024-position-valuation.up.sql
-- What a position is worth at a date. Reported by the user or imported today; generated from quotes
-- later. The latest one is the position's current value; the history feeds returns later.

CREATE TABLE finances.fin024_position_valuation (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	position_id uuid NOT NULL,
	valued_on DATE NOT NULL,
	value NUMERIC(18, 2) NOT NULL,
	source VARCHAR(20) NOT NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin024_position_valuation
ADD CONSTRAINT pk_fin024 PRIMARY KEY (id);

ALTER TABLE finances.fin024_position_valuation
ADD CONSTRAINT fk_fin024_position_id FOREIGN KEY (position_id) REFERENCES finances.fin022_position (id);

ALTER TABLE finances.fin024_position_valuation
ADD CONSTRAINT uq_fin024_position_valued_on UNIQUE (position_id, valued_on);

ALTER TABLE finances.fin024_position_valuation
ADD CONSTRAINT ck_fin024_source CHECK (source IN ('manual', 'import', 'quote'));

ALTER TABLE finances.fin024_position_valuation
ADD CONSTRAINT ck_fin024_value CHECK (value >= 0);
