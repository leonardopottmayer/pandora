CREATE TABLE notes.nte017_daily_note (
    id uuid NOT NULL DEFAULT uuid_generate_v7(),
    user_id uuid NOT NULL,
    date date NOT NULL,
    page_id uuid NOT NULL,
    CONSTRAINT pk_nte017 PRIMARY KEY (id),
    CONSTRAINT uq_nte017_user_date UNIQUE (user_id, date)
);
-- No page FK: the registry survives purge and points to a replacement on the next visit.
