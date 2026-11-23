-- 20261123130003-create-table-fin022-position.up.sql
-- An asset inside an account. 'unit' positions keep a quantity and an average cost, one open position
-- per asset and account. 'application' positions are fixed income, one per application, with the
-- amount still invested and the application's terms (informational until quotes and curves exist).

CREATE TABLE finances.fin022_position (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	account_id uuid NOT NULL,
	asset_id uuid NOT NULL,
	behavior VARCHAR(20) NOT NULL,
	status VARCHAR(20) NOT NULL,
	quantity NUMERIC(28, 10) NULL,
	average_cost NUMERIC(28, 10) NULL,
	invested_amount NUMERIC(18, 2) NULL,
	applied_on DATE NULL,
	index VARCHAR(20) NULL,
	index_percent NUMERIC(9, 4) NULL,
	spread_rate NUMERIC(9, 4) NULL,
	maturity_on DATE NULL,
	closed_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin022_position
ADD CONSTRAINT pk_fin022 PRIMARY KEY (id);

ALTER TABLE finances.fin022_position
ADD CONSTRAINT fk_fin022_account_id FOREIGN KEY (account_id) REFERENCES finances.fin001_account (id);

ALTER TABLE finances.fin022_position
ADD CONSTRAINT fk_fin022_asset_id FOREIGN KEY (asset_id) REFERENCES finances.fin021_asset (id);

ALTER TABLE finances.fin022_position
ADD CONSTRAINT ck_fin022_behavior CHECK (behavior IN ('unit', 'application'));

ALTER TABLE finances.fin022_position
ADD CONSTRAINT ck_fin022_status CHECK (status IN ('open', 'closed'));

ALTER TABLE finances.fin022_position
ADD CONSTRAINT ck_fin022_index CHECK (index IS NULL OR index IN ('cdi', 'selic', 'ipca', 'pre', 'other'));

-- Each behavior fills its own fields and leaves the other's empty.
ALTER TABLE finances.fin022_position
ADD CONSTRAINT ck_fin022_behavior_fields CHECK (
	(behavior = 'unit'
		AND quantity IS NOT NULL AND average_cost IS NOT NULL
		AND invested_amount IS NULL AND applied_on IS NULL AND index IS NULL
		AND index_percent IS NULL AND spread_rate IS NULL AND maturity_on IS NULL)
	OR
	(behavior = 'application'
		AND quantity IS NULL AND average_cost IS NULL
		AND invested_amount IS NOT NULL AND applied_on IS NOT NULL));

CREATE UNIQUE INDEX uq_fin022_open_unit
ON finances.fin022_position (account_id, asset_id) WHERE behavior = 'unit' AND status = 'open';

CREATE INDEX ix_fin022_user_id
ON finances.fin022_position (user_id);

CREATE INDEX ix_fin022_account_id
ON finances.fin022_position (account_id);
