-- Provision Pandora's database and role in a shared tier Postgres.
-- Run ONCE per tier, as the superuser, passing the app role's password:
--
--   Get-Content infra/postgres/bootstrap-pandora.sql |
--     docker compose -p staging-db -f infra/postgres/docker-compose.yml `
--       --env-file infra/postgres/.env.staging `
--       exec -T db psql -U postgres -d postgres -v pandora_password='<PANDORA_DB_PASSWORD>'
--
-- The password comes from POSTGRES_PASSWORD in the Pandora env file.

CREATE ROLE pandora LOGIN PASSWORD :'pandora_password';
CREATE DATABASE pottmayer_pandora OWNER pandora;
