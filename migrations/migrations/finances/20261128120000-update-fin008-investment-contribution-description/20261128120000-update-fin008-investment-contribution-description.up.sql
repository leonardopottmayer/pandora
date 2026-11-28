-- 20261128120000-update-fin008-investment-contribution-description.up.sql
-- Entries created by a buy into an application read "Contribution" ("Aporte"), as new ones do.

UPDATE finances.fin008_transaction t
SET system_description = jsonb_set(t.system_description, '{Key}', '"transaction.investment-contribution"')
FROM finances.fin023_position_movement m
JOIN finances.fin022_position p ON p.id = m.position_id
WHERE m.transaction_id = t.id
	AND m.kind = 'buy'
	AND p.behavior = 'application'
	AND t.system_description ->> 'Key' = 'transaction.investment-buy';
