-- 20261123130002-create-table-fin021-asset.up.sql
-- Something the user owns: a stock, a CDB product, a coin. Its currency must match the account that
-- holds it (conversion between currencies is a later part).

CREATE TABLE finances.fin021_asset (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	asset_class_id uuid NOT NULL,
	name VARCHAR(100) NOT NULL,
	code VARCHAR(20) NULL,
	isin VARCHAR(12) NULL,
	issuer VARCHAR(100) NULL,
	currency VARCHAR(10) NOT NULL,
	archived_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin021_asset
ADD CONSTRAINT pk_fin021 PRIMARY KEY (id);

ALTER TABLE finances.fin021_asset
ADD CONSTRAINT fk_fin021_asset_class_id FOREIGN KEY (asset_class_id) REFERENCES finances.fin020_asset_class (id);

ALTER TABLE finances.fin021_asset
ADD CONSTRAINT uq_fin021_user_name UNIQUE (user_id, name);

CREATE UNIQUE INDEX uq_fin021_user_class_code
ON finances.fin021_asset (user_id, asset_class_id, code) WHERE code IS NOT NULL;
