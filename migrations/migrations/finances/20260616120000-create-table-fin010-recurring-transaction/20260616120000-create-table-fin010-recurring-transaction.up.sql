-- 20260616120000-create-table-fin010-recurring-transaction.up.sql

CREATE TABLE finances.fin010_recurring_transaction (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	user_id uuid NOT NULL,
	name varchar(100) NOT NULL,
	-- template
	account_id uuid NULL,
	card_id uuid NULL,
	kind varchar(30) NOT NULL,
	amount numeric(20,8) NULL,
	amount_is_estimate boolean NOT NULL DEFAULT false,
	description varchar(255) NOT NULL,
	payee varchar(150) NULL,
	system_category_id uuid NULL,
	user_category_id uuid NULL,
	-- recurrence rule
	frequency varchar(10) NOT NULL,
	interval smallint NOT NULL DEFAULT 1,
	day_of_month smallint NULL,
	weekday smallint NULL,
	start_date date NOT NULL,
	end_date date NULL,
	max_occurrences int NULL,
	-- execution
	status varchar(10) NOT NULL DEFAULT 'active',
	auto_post boolean NOT NULL DEFAULT false,
	next_occurrence_on date NOT NULL,
	occurrences_count int NOT NULL DEFAULT 0,
	-- audit
	created_by uuid NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT current_timestamp,
	updated_by uuid NULL,
	updated_at TIMESTAMPTZ NULL
);

ALTER TABLE finances.fin010_recurring_transaction
ADD CONSTRAINT pk_fin010 PRIMARY KEY (id);

ALTER TABLE finances.fin010_recurring_transaction
ADD CONSTRAINT ck_fin010_status CHECK (status IN ('active', 'paused', 'finished'));

ALTER TABLE finances.fin010_recurring_transaction
ADD CONSTRAINT ck_fin010_frequency CHECK (frequency IN ('daily', 'weekly', 'monthly', 'yearly'));

ALTER TABLE finances.fin010_recurring_transaction
ADD CONSTRAINT ck_fin010_interval CHECK (interval >= 1);

ALTER TABLE finances.fin010_recurring_transaction
ADD CONSTRAINT ck_fin010_day_of_month CHECK (day_of_month BETWEEN 1 AND 31);

ALTER TABLE finances.fin010_recurring_transaction
ADD CONSTRAINT ck_fin010_weekday CHECK (weekday BETWEEN 0 AND 6);

ALTER TABLE finances.fin010_recurring_transaction
ADD CONSTRAINT ck_fin010_destination CHECK (
	(account_id IS NOT NULL AND card_id IS NULL) OR
	(account_id IS NULL AND card_id IS NOT NULL)
);

ALTER TABLE finances.fin010_recurring_transaction
ADD CONSTRAINT fk_fin010_account_id FOREIGN KEY (account_id)
	REFERENCES finances.fin001_account (id);

ALTER TABLE finances.fin010_recurring_transaction
ADD CONSTRAINT fk_fin010_card_id FOREIGN KEY (card_id)
	REFERENCES finances.fin006_card (id);

CREATE INDEX ix_fin010_user_id
ON finances.fin010_recurring_transaction (user_id);

CREATE INDEX ix_fin010_status_next_occurrence
ON finances.fin010_recurring_transaction (status, next_occurrence_on);
