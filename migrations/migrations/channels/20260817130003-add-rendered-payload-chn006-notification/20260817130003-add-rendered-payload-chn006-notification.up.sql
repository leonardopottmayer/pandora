-- 20260817130003-add-rendered-payload-chn006-notification.up.sql

-- Structured, already-rendered content for channels that e-mail's subject/body/is_html cannot
-- express -- today, a Telegram inline keyboard. Null for e-mail, which keeps using the columns it
-- always had.
ALTER TABLE channels.chn006_notification
ADD COLUMN rendered_payload JSONB NULL;
