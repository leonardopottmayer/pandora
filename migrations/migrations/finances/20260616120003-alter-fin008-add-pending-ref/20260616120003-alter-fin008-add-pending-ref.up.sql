-- 20260616120003-alter-fin008-add-pending-ref.up.sql
-- The one unavoidable ALTER: fin008 and fin011 reference each other, so whichever is created first
-- cannot inline its FK to the other. fin011 references fin008 inline (created second); fin008's
-- back-reference to fin011 (pending_transaction_id) is added here, once fin011 exists. The column
-- and its index already live in fin008's create -- only the constraint is deferred.

ALTER TABLE finances.fin008_transaction
ADD CONSTRAINT fk_fin008_pending_transaction_id FOREIGN KEY (pending_transaction_id)
	REFERENCES finances.fin011_pending_transaction (id);
