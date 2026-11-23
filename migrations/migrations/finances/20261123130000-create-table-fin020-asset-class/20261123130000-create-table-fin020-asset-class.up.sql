-- 20261123130000-create-table-fin020-asset-class.up.sql
-- What kind of investment an asset is (CDB, stock, FII...). System classes have user_id NULL and are
-- seeded; users add their own. The behavior decides how a position works: 'unit' keeps a quantity and
-- an average cost, 'application' is fixed income with its own date, rate and maturity.

CREATE TABLE finances.fin020_asset_class (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NULL,
	code VARCHAR(40) NULL,
	name VARCHAR(60) NOT NULL,
	behavior VARCHAR(20) NOT NULL,
	archived_at TIMESTAMPTZ NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin020_asset_class
ADD CONSTRAINT pk_fin020 PRIMARY KEY (id);

ALTER TABLE finances.fin020_asset_class
ADD CONSTRAINT ck_fin020_behavior CHECK (behavior IN ('unit', 'application'));

-- System classes carry a code; user classes never do.
ALTER TABLE finances.fin020_asset_class
ADD CONSTRAINT ck_fin020_code CHECK ((user_id IS NULL) = (code IS NOT NULL));

CREATE UNIQUE INDEX uq_fin020_code
ON finances.fin020_asset_class (code) WHERE code IS NOT NULL;

CREATE UNIQUE INDEX uq_fin020_user_name
ON finances.fin020_asset_class (user_id, name) WHERE user_id IS NOT NULL;
