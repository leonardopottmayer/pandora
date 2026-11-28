-- 20261128120000-update-fin008-investment-contribution-description.down.sql
-- Back to the generic "Buy" description.

UPDATE finances.fin008_transaction
SET system_description = jsonb_set(system_description, '{Key}', '"transaction.investment-buy"')
WHERE system_description ->> 'Key' = 'transaction.investment-contribution';
