-- 20261007120001-create-extension-pg-trgm.up.sql

-- Trigram matching for file-name search: names are found by fragments ("breaking bad s02"), not by
-- words, so full-text search does not fit. Trusted extension: the database owner can create it.
CREATE EXTENSION IF NOT EXISTS pg_trgm;
