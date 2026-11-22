-- 20261027120002-create-table-com002-mail-label.up.sql

-- A Gmail label or an IMAP folder. provider_label_id is the Gmail label id or the folder's full path.
-- The IMAP per-folder cursor (UIDVALIDITY, UIDNEXT, MODSEQ) is not here: it lives inside the
-- mailbox's opaque sync_cursor, which only the IMAP adapter reads.
CREATE TABLE communications.com002_mail_label (
	id uuid NOT NULL DEFAULT uuid_generate_v7(),
	mailbox_id uuid NOT NULL,
	provider_label_id TEXT NOT NULL,
	name TEXT NOT NULL,
	kind VARCHAR(20) NOT NULL,
	role VARCHAR(20) NULL,
	is_exclusive BOOLEAN NOT NULL
);

ALTER TABLE communications.com002_mail_label
ADD CONSTRAINT pk_com002 PRIMARY KEY (id);

ALTER TABLE communications.com002_mail_label
ADD CONSTRAINT uq_com002_mailbox_provider_label UNIQUE (mailbox_id, provider_label_id);

ALTER TABLE communications.com002_mail_label
ADD CONSTRAINT fk_com002_mailbox_id FOREIGN KEY (mailbox_id)
REFERENCES communications.com001_mailbox (id) ON DELETE CASCADE;

ALTER TABLE communications.com002_mail_label
ADD CONSTRAINT chk_com002_kind
CHECK (kind IN ('system', 'user'));

ALTER TABLE communications.com002_mail_label
ADD CONSTRAINT chk_com002_role
CHECK (role IN ('inbox', 'sent', 'drafts', 'trash', 'spam', 'archive'));
