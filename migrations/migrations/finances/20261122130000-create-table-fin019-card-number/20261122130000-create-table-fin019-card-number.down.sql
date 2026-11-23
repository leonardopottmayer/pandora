-- 20261122130000-create-table-fin019-card-number.down.sql
-- fin006.last_four was kept untouched, so the cards keep the last four digits they had before.
-- Numbers added after the up migration are lost.

DROP TABLE finances.fin019_card_number;
