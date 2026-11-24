-- 20261123130000-create-table-fin020-system-asset-class.up.sql
-- The asset classes the system ships (CDB, stock, FII...): global, seeded, read-only, like the system
-- categories (fin002). The user's own classes live in fin025, as user categories live in fin003. The
-- behavior decides how a position works: 'unit' keeps a quantity and an average cost, 'application'
-- is fixed income with its own date, rate and maturity.

CREATE TABLE finances.fin020_system_asset_class (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	code VARCHAR(40) NOT NULL,
	name VARCHAR(60) NOT NULL,
	behavior VARCHAR(20) NOT NULL,
	display_order INT NOT NULL,
	created_by UUID NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by UUID NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin020_system_asset_class
ADD CONSTRAINT pk_fin020 PRIMARY KEY (id);

ALTER TABLE finances.fin020_system_asset_class
ADD CONSTRAINT uq_fin020_code UNIQUE (code);

ALTER TABLE finances.fin020_system_asset_class
ADD CONSTRAINT ck_fin020_behavior CHECK (behavior IN ('unit', 'application'));
