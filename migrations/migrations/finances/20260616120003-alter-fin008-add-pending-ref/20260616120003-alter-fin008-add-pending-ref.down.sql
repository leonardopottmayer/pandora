-- 20260616120003-alter-fin008-add-pending-ref.down.sql

ALTER TABLE finances.fin008_transaction
DROP CONSTRAINT fk_fin008_pending_transaction_id;
