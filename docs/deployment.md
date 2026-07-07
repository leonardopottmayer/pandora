# Deployment (containers)

Operational runbook for building and running Pandora with Docker on the homelab.
For the roadmap and rationale, see [homelab-deploy.md](homelab-deploy.md).

Everything here is reproducible from a clean checkout — follow
[From zero to running](#from-zero-to-running) top to bottom.

---

## Architecture

`docker-compose.yml` is the **Pandora app stack** (one per environment):

| Service | Image | Role |
|---|---|---|
| `backend` | `pandora-backend` (GHCR) | .NET API, HTTP on `8080` **inside** the Docker network (no host port). |
| `frontend` | `pandora-frontend` (GHCR) | nginx: serves the built SPA and proxies `/api` -> `backend:8080` (same origin, no CORS). |
| `mailhog` | `mailhog/mailhog` | Email catcher. **Profile-gated** (`COMPOSE_PROFILES=mailhog`) — runs in staging, not prod. SMTP on `1025` (internal); web UI exposed to the LAN. |

The frontend is built with an empty `VITE_API_URL`, so the browser talks to the
same origin and nginx forwards `/api` to the backend.

### Database topology

Postgres is **not** in the app stack. Each homelab **tier** (staging, prod) runs
one shared Postgres — `infra/postgres` — that every app in that tier connects to,
each with its own database and role:

| Tier | DB stack | Network | Host alias | Localhost port |
|---|---|---|---|---|
| Staging | `staging-db` | `staging` | `staging-db` | `5433` |
| Production | `prod-db` | `prod` | `prod-db` | `5432` |

The app's `backend` joins the tier network and connects to `${DB_HOST}` using
Pandora's own role (`POSTGRES_USER`/`POSTGRES_PASSWORD`) and database
(`POSTGRES_DB`). The DB stack's superuser is separate and used only to provision
per-app databases/roles.

**Config override via env vars.** ASP.NET Core maps `__` (double underscore) in an
environment variable to `:` in a config key. That is why the compose file sets
e.g. `Tars__Data__Connections__identity__ConnectionString` — it overrides
`Tars:Data:Connections:identity:ConnectionString` from `appsettings.json`.

### Files that make up this setup

| File | Purpose |
|---|---|
| `VERSION` | Single source of truth for the version string. |
| `backend/Directory.Build.props` | Reads `VERSION` into the MSBuild `Version` of every backend project. |
| `backend/Dockerfile` | Multi-stage build (SDK -> runtime). Restores the private NuGet feed using a build secret. |
| `backend/nuget.config` | Declares the package sources: nuget.org + the private GitHub feed. |
| `client-web/Dockerfile` | Builds the Vite app and serves it with nginx. |
| `client-web/nginx.conf` | SPA fallback + `/api` reverse proxy to the backend. |
| `docker-compose.yml` | The Pandora app stack. Project name, ports, secrets from an env file. |
| `.env.example` | Template for the per-environment app env files. |
| `infra/postgres/docker-compose.yml` | Shared per-tier Postgres stack. |
| `infra/postgres/.env.example` | Template for the DB stack's per-tier env file. |
| `infra/postgres/bootstrap-pandora.sql` | Creates Pandora's role + database in the shared DB. |
| `.github/workflows/build-images.yml` | CI: builds both images and pushes to GHCR on push to `main`. |
| `.dockerignore`, `client-web/.dockerignore` | Keep build contexts small; **exclude `.env`** so no dev config is baked in. |

---

## Prerequisites

On the homelab (to run):
- Docker Engine + Compose v2.

For building locally or developing (optional — CI builds the images normally):
- .NET 10 SDK, Node 24.
- `migris` CLI (for database migrations).

GitHub:
- A classic Personal Access Token with **only** the `read:packages` scope
  (used both for the CI restore and for pulling images on the homelab).
  Classic is used because the GitHub **NuGet** registry has unreliable
  fine-grained-token support.

---

## One-time setup

### 1. CI token (restore of the private Tars packages)

The `Pottmayer.Tars.*` packages live in a **different** repo, so the automatic
`GITHUB_TOKEN` in Actions cannot read them.

1. GitHub → Settings → Developer settings → **Tokens (classic)** → generate one
   with scope **`read:packages`**.
2. Pandora repo → Settings → Secrets and variables → **Actions** →
   **Repository secret** named exactly `NUGET_GITHUB_TOKEN`.

The workflow passes it to the backend build as the Docker build secret
`github_token` (never baked into the image or printed in logs).

### 2. Homelab GHCR login (to pull the images)

Images pushed to GHCR are **private by default**, even though the repo is public.
Keep them private and authenticate the homelab once:

```bash
echo "<PAT with read:packages>" | docker login ghcr.io -u leonardopottmayer --password-stdin
```

### 3. Per-environment env files

Each environment is one env file (gitignored). Create them from the template:

```bash
cp .env.example .env.prod       # COMPOSE_PROJECT_NAME=pandora-prod,    HTTP_PORT=8730, POSTGRES_PORT=5432
cp .env.example .env.staging    # COMPOSE_PROJECT_NAME=pandora-staging, HTTP_PORT=8731, POSTGRES_PORT=5433
```

Generate strong secrets and fill each file:

```bash
openssl rand -hex 24      # POSTGRES_PASSWORD (hex: no +/= that could break the connection string)
openssl rand -base64 48   # JWT_SIGNING_KEY
openssl rand -base64 32   # MFA_ENCRYPTION_KEY
```

| Variable | What it does |
|---|---|
| `COMPOSE_PROJECT_NAME` | Isolates containers/network/volumes between environments. |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment (`Production` for real deploys). |
| `COMPOSE_PROFILES` | `mailhog` to run the email catcher (staging); empty in prod. |
| `TIER_NETWORK` / `DB_HOST` | Which shared tier DB to use (`staging`/`staging-db`, `prod`/`prod-db`). |
| `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` | Pandora's own role + database in the shared DB (all three connection strings). |
| `JWT_SIGNING_KEY` | Overrides `Tars:Security:Identity:Jwt:SigningKey`. |
| `MFA_ENCRYPTION_KEY` | Overrides `Pandora:Identity:Mfa:EncryptionKey`. |
| `SMTP_HOST` / `SMTP_PORT` | Backend SMTP target. Staging: `mailhog`/`1025`; prod: real provider. |
| `MAILHOG_UI_PORT` | Host port for the Mailhog web UI (only when the profile is on). |
| `BACKEND_IMAGE` / `FRONTEND_IMAGE` | Which image tag to run (prod: pinned; staging: `:latest`). |
| `HTTP_PORT` | The LAN-visible frontend port. Unique per env. |

Keep a copy of each env file in Bitwarden. **Never** commit real secrets.

### 4. Shared tier database (once per tier)

```bash
# Create the shared network for the tier
docker network create staging

# Configure and start the tier's Postgres
cp infra/postgres/.env.example infra/postgres/.env.staging   # edit superuser pw, DB_PORT, names
docker compose --env-file infra/postgres/.env.staging -f infra/postgres/docker-compose.yml up -d

# Provision Pandora's database + role (uses POSTGRES_PASSWORD from .env.staging)
Get-Content infra/postgres/bootstrap-pandora.sql | \
  docker compose -p staging-db -f infra/postgres/docker-compose.yml \
    --env-file infra/postgres/.env.staging \
    exec -T db psql -U postgres -d postgres -v pandora_password='<PANDORA_DB_PASSWORD>'
```

Repeat with `prod` / `.env.prod` / port `5432` for production.

---

## Versioning

`VERSION` at the repo root is the single source of truth;
`backend/Directory.Build.props` injects it into every backend project. Bump it
before cutting a release, so the CI publishes images tagged with the new version.

---

## Environments & ports

Multiple environments share the **same images** and the **same compose file**;
only the env file differs. Each is isolated by its Compose project name.

| Environment | `COMPOSE_PROJECT_NAME` | `HTTP_PORT` | Image tag | Data |
|---|---|---|---|---|
| Production | `pandora-prod` | `8730` | pinned (e.g. `:0.1.0`) | real, backed up |
| Staging | `pandora-staging` | `8731` | `:latest` | disposable |

`HTTP_PORT` is the app stack's only LAN-visible port (plus `MAILHOG_UI_PORT` in
staging). The DB port lives in the tier DB stack (see Database topology). In
phase 2, app access moves to a hostname via Cloudflare Tunnel.

---

## CI (GitHub Actions)

`.github/workflows/build-images.yml` runs on every push to `main` (and manual
dispatch):

- Builds `backend` and `frontend` in parallel and pushes to GHCR.
- Tags per image: `latest`, the `VERSION` value (e.g. `0.1.0`), and `sha-<commit>`.
- GHCR push uses the automatic `GITHUB_TOKEN` (`packages: write`).
- The backend restore uses the `NUGET_GITHUB_TOKEN` repo secret (see setup above).
- Layer cache via `type=gha` speeds up later builds.

The first sign the token works: the backend job's build step gets past
`dotnet restore` without a `401`.

---

## From zero to running

On the homelab, from a clean checkout. Example uses **staging** (the first
environment to bring up); swap `.env.staging` for `.env.prod` to do production.

```bash
# 0. One-time: docker login ghcr.io, and bring up the shared tier DB +
#    bootstrap Pandora's role/database (see "One-time setup" above).

# 1. Pull the images built by CI
docker compose --env-file .env.staging pull

# 2. Start the app stack (connects to the shared staging-db)
docker compose --env-file .env.staging up -d

# 3. Apply database migrations (see "Migrations" below)
cd migrations && migris apply staging -y && cd ..

# 4. Open the app on the LAN
#    http://<homelab-ip>:8731        (Mailhog: http://<homelab-ip>:8732)
```

Building locally instead of pulling (optional):

```bash
export GITHUB_TOKEN=<PAT with read:packages>
docker compose --env-file .env.staging \
  build --secret id=github_token,env=GITHUB_TOKEN backend
docker compose --env-file .env.staging build frontend
```

---

## Migrations

`migris` runs on the homelab host and connects over TCP, which is why the tier DB
publishes a `127.0.0.1` port. Point migris at `127.0.0.1:<DB_PORT>` (staging
`5433`, prod `5432`) with Pandora's role/database (`pandora` / `pottmayer_pandora`).

> The DB credentials are secrets, so `migrations/config.json` is **gitignored**
> (a `migrations/config.example.json` template is committed). Copy the template
> and add a `staging`/`prod` environment pointing at the tier DB port with
> Pandora's role.

Run migrations after `up -d`, and again whenever a deploy includes new migration
files.

---

## Email

Staging captures all outgoing mail with **Mailhog** instead of sending for real
(single user, no need for real delivery yet):

- The backend sends to `mailhog:1025` (set via `SMTP_HOST`/`SMTP_PORT`).
- Mailhog only runs when `COMPOSE_PROFILES=mailhog` is set (staging's env file).
- Read captured emails at the web UI: `http://<homelab-ip>:${MAILHOG_UI_PORT}`
  (staging default `8732`).

For **prod**, leave `COMPOSE_PROFILES` empty and point `SMTP_HOST`/`SMTP_PORT`
(plus, when needed, TLS/credentials in `appsettings.json` or extra env vars) at a
real provider — see phase 2 in [homelab-deploy.md](homelab-deploy.md).

---

## Day-2 operations

```bash
# Update to the latest built images
docker compose --env-file .env.prod pull
docker compose --env-file .env.prod up -d

# Inspect
docker compose --env-file .env.prod ps
docker compose --env-file .env.prod logs -f backend

# Stop (KEEPS the data volume)
docker compose --env-file .env.prod down
```

Swap `.env.prod` for `.env.staging` to operate the other environment — both can
run at once on different ports.

> `down` on the app stack removes only app containers — the data lives in the
> tier DB stack. The Postgres volume is deleted only by running `down -v` on the
> **DB** stack (`infra/postgres`), never by app-stack commands.

For reproducible prod deploys, pin `BACKEND_IMAGE` / `FRONTEND_IMAGE` to the
`VERSION` tag rather than `:latest`.

---

## Security notes

- **Images stay private** on GHCR; the homelab authenticates with a
  `read:packages` token to pull them.
- **Secrets never enter Git.** Env files are gitignored; the values in
  `appsettings.json` are dev defaults that prod overrides via the env file.
- The CI/pull token is limited to `read:packages` (read-only, packages only).
- The workflow triggers on `push`/`workflow_dispatch` only, so fork pull
  requests never receive the secret.

---

## Reverse proxy readiness

The backend runs `UseForwardedHeaders` (trusts `X-Forwarded-For` / `-Proto`) and
only forces HTTPS redirection in Development. TLS is terminated by the proxy
(nginx now, Cloudflare in phase 2), so exposing it later needs no app changes.
