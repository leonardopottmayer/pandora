-- 20261027120004-create-table-com004-mail-message-label.up.sql

-- The labels a message carries (on IMAP, exactly one: its folder).
CREATE TABLE communications.com004_mail_message_label (
	message_id uuid NOT NULL,
	label_id uuid NOT NULL
);

ALTER TABLE communications.com004_mail_message_label
ADD CONSTRAINT pk_com004 PRIMARY KEY (message_id, label_id);

ALTER TABLE communications.com004_mail_message_label
ADD CONSTRAINT fk_com004_message_id FOREIGN KEY (message_id)
REFERENCES communications.com003_mail_message (id) ON DELETE CASCADE;

ALTER TABLE communications.com004_mail_message_label
ADD CONSTRAINT fk_com004_label_id FOREIGN KEY (label_id)
REFERENCES communications.com002_mail_label (id) ON DELETE CASCADE;

-- The messages under one label.
CREATE INDEX ix_com004_label_message ON communications.com004_mail_message_label (label_id, message_id);
