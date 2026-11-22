-- 20261027120003-create-table-com003-mail-message.up.sql

-- The mirrored header of one message. provider_message_id is the Gmail message id or, on IMAP,
-- "{uidvalidity}:{uid}:{folder path}" (UIDs are per folder). The body lives in com005, fetched on demand.
-- to_recipients and cc_recipients: [{ "Name": ..., "Address": ... }].
CREATE TABLE communications.com003_mail_message (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	mailbox_id uuid NOT NULL,
	provider_message_id TEXT NOT NULL,
	thread_key TEXT NOT NULL,
	rfc822_message_id TEXT NULL,
	from_name TEXT NULL,
	from_address VARCHAR(320) NULL,
	to_recipients JSONB NOT NULL DEFAULT '[]',
	cc_recipients JSONB NOT NULL DEFAULT '[]',
	subject TEXT NOT NULL DEFAULT '',
	snippet TEXT NOT NULL DEFAULT '',
	sent_at TIMESTAMPTZ NOT NULL,
	is_unread BOOLEAN NOT NULL,
	is_starred BOOLEAN NOT NULL,
	has_attachments BOOLEAN NOT NULL,
	size_bytes BIGINT NOT NULL DEFAULT 0
);

ALTER TABLE communications.com003_mail_message
ADD CONSTRAINT pk_com003 PRIMARY KEY (id);

ALTER TABLE communications.com003_mail_message
ADD CONSTRAINT uq_com003_mailbox_provider_message UNIQUE (mailbox_id, provider_message_id);

ALTER TABLE communications.com003_mail_message
ADD CONSTRAINT fk_com003_mailbox_id FOREIGN KEY (mailbox_id)
REFERENCES communications.com001_mailbox (id) ON DELETE CASCADE;

-- The message list: newest first, keyset on (sent_at, id).
CREATE INDEX ix_com003_mailbox_sent_at ON communications.com003_mail_message (mailbox_id, sent_at DESC, id DESC);

-- "Do we already have this one?" before publishing MailMessageReceived.
CREATE INDEX ix_com003_mailbox_rfc822_message_id ON communications.com003_mail_message (mailbox_id, rfc822_message_id);

-- One conversation.
CREATE INDEX ix_com003_mailbox_thread_key ON communications.com003_mail_message (mailbox_id, thread_key);
