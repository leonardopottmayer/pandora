-- 20261027120005-create-table-com005-mail-content.up.sql

-- The cached body of an opened message. html_body is kept as received and sanitized when rendered.
-- attachments: [{ "Id": ..., "FileName": ..., "ContentType": ..., "SizeBytes": ..., "ContentId": ... }].
-- Rows not accessed for ContentCacheRetentionDays are deleted by the sync job.
CREATE TABLE communications.com005_mail_content (
	message_id uuid NOT NULL,
	text_body TEXT NULL,
	html_body TEXT NULL,
	attachments JSONB NOT NULL DEFAULT '[]',
	fetched_at TIMESTAMPTZ NOT NULL,
	last_accessed_at TIMESTAMPTZ NOT NULL
);

ALTER TABLE communications.com005_mail_content
ADD CONSTRAINT pk_com005 PRIMARY KEY (message_id);

ALTER TABLE communications.com005_mail_content
ADD CONSTRAINT fk_com005_message_id FOREIGN KEY (message_id)
REFERENCES communications.com003_mail_message (id) ON DELETE CASCADE;

CREATE INDEX ix_com005_last_accessed_at ON communications.com005_mail_content (last_accessed_at);
