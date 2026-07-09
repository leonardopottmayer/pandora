-- Provision Pandora's database and role in a homelab tier's shared Postgres.
-- Run ONCE per tier, as the superuser, passing the app role's password.
-- The tier DB itself is set up from the homelab repo (see its docs).
--
-- CLI (from the Pandora repo root, on the homelab host):
--   Get-Content deploy/bootstrap-pandora.sql | `
--     docker exec -i staging-db psql -U postgres -d postgres -v pandora_password='<PANDORA_DB_PASSWORD>'
--   (use prod-db for prod)
--
-- Or paste it into the tier server's Query Tool in pgAdmin.
--
-- The password must match POSTGRES_PASSWORD in the Pandora env file (.env.<tier>).

CREATE ROLE pandora LOGIN PASSWORD :'pandora_password';
CREATE DATABASE pandora OWNER pandora;
