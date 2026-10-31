-- 20261031120000-create-table-com006-mail-rule.up.sql

-- One rule of a mailbox. execution says where it runs: provider-filter is a native Gmail filter, with
-- provider_filter_id its id there (null while disabled, since Gmail has no disabled filters); pandora runs in
-- Pandora's own engine, on mail that arrives.
-- conditions: [{ "Kind": ..., "Value": ... }], all must match. actions: [{ "Kind": ..., "LabelId": ... }].
-- status read-only is a Gmail filter holding what the model does not (a forward, a size): provider_filter_raw keeps
-- it as Gmail sent it, and Pandora never rewrites it.
CREATE TABLE communications.com006_mail_rule (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	mailbox_id uuid NOT NULL,
	name VARCHAR(100) NOT NULL,
	position INTEGER NOT NULL,
	conditions JSONB NOT NULL DEFAULT '[]',
	actions JSONB NOT NULL DEFAULT '[]',
	execution VARCHAR(20) NOT NULL,
	provider_filter_id TEXT NULL,
	provider_filter_raw JSONB NULL,
	is_enabled BOOLEAN NOT NULL,
	status VARCHAR(20) NOT NULL,
	last_error TEXT NULL
);

ALTER TABLE communications.com006_mail_rule
ADD CONSTRAINT pk_com006 PRIMARY KEY (id);

ALTER TABLE communications.com006_mail_rule
ADD CONSTRAINT fk_com006_mailbox_id FOREIGN KEY (mailbox_id)
REFERENCES communications.com001_mailbox (id) ON DELETE CASCADE;

ALTER TABLE communications.com006_mail_rule
ADD CONSTRAINT chk_com006_execution
CHECK (execution IN ('provider-filter', 'pandora'));

ALTER TABLE communications.com006_mail_rule
ADD CONSTRAINT chk_com006_status
CHECK (status IN ('ok', 'broken', 'read-only'));

-- One rule per Gmail filter.
CREATE UNIQUE INDEX uq_com006_mailbox_provider_filter ON communications.com006_mail_rule (mailbox_id, provider_filter_id)
WHERE provider_filter_id IS NOT NULL;

-- A mailbox's rules, in order.
CREATE INDEX ix_com006_mailbox_position ON communications.com006_mail_rule (mailbox_id, position);
