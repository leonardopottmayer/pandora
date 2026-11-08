CREATE TABLE notes.nte015_note_capture (
    -- The Channels inbound event id is the natural idempotency key.
    id UUID NOT NULL,
    user_id UUID NOT NULL,
    -- Retained after a page is purged, so a replay cannot resurrect a deleted capture.
    page_id UUID NOT NULL,
    captured_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT pk_nte015 PRIMARY KEY (id)
);
CREATE INDEX ix_nte015_user_page ON notes.nte015_note_capture (user_id, page_id);
