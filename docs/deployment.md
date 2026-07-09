# Deployment (containers)

Operational runbook for building and running Pandora with Docker on the homelab.
For the roadmap and rationale, see [homelab-deploy.md](homelab-deploy.md).

Everything here is reproducible from a clean checkout — follow
[Creating an environment](#creating-an-environment-step-by-step) top to bottom.

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

Postgres is **not** in the app stack, and not in this repo. Each homelab **tier**
(staging, prod) runs one shared Postgres, set up from the **homelab repo**
(`G:\dev\homelab`), that every app in that tier connects to — each with its own
database and role:

| Tier | Container | Network | Host alias | Localhost port |
|---|---|---|---|---|
| Staging | `staging-db` | `staging` | `staging-db` | `5433` |
| Production | `prod-db` | `prod` | `prod-db` | `5432` |

The app's `backend` joins the tier network and connects to `${DB_HOST}` using
Pandora's own role (`POSTGRES_USER`/`POSTGRES_PASSWORD`) and database
(`POSTGRES_DB`). The shared DB's superuser lives in the homelab repo and is used
only to provision per-app databases/roles.

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
| `deploy/bootstrap-pandora.sql` | Creates Pandora's role + database in the tier's shared Postgres. |
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
Keep them private and authenticate the homelab once (PowerShell):

```powershell
docker login ghcr.io -u leonardopottmayer   # paste the PAT (read:packages) as the password
```

The full step-by-step for bringing up an environment is in
[Creating an environment](#creating-an-environment-step-by-step). The two env
files it uses:

**App env file** (`.env.<tier>`, gitignored):

| Variable | What it does |
|---|---|
| `COMPOSE_PROJECT_NAME` | Isolates the app stack per environment (`pandora-staging`, `pandora-prod`). |
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

The shared tier Postgres (the `${DB_HOST}` the app connects to) is **not** part of
this repo — it's set up once per tier from the homelab repo (`G:\dev\homelab`).
Bring it up before deploying Pandora into that tier.

Keep a copy of every env file in Bitwarden. **Never** commit real secrets.

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

## Creating an environment (step by step)

Commands are **PowerShell** (the homelab is Windows), run from the Pandora repo
root. The worked example is **staging**; per-tier values:

| | staging | prod |
|---|---|---|
| Tier network | `staging` | `prod` |
| DB container / host alias | `staging-db` | `prod-db` |
| DB localhost port | `5433` | `5432` |
| App env file | `.env.staging` | `.env.prod` |
| Frontend port | `8731` | `8730` |
| Mailhog | yes (UI `8732`) | no |

Prereqs done once per machine: the [CI token](#1-ci-token-restore-of-the-private-tars-packages)
and `docker login ghcr.io` (see One-time setup).

### 0. Ensure the homelab tier is up

The tier network + shared Postgres come from the **homelab repo**
(`G:\dev\homelab`): create the `staging` network and bring up `staging-db` per its
docs. Confirm the container is running before continuing:

```powershell
docker ps --filter name=staging-db
```

### 1. Generate secrets

```powershell
function New-Secret([int]$bytes, [string]$fmt = 'base64') {
  $b = New-Object byte[] $bytes
  (New-Object System.Security.Cryptography.RNGCryptoServiceProvider).GetBytes($b)
  if ($fmt -eq 'hex') { ($b | ForEach-Object { $_.ToString('x2') }) -join '' }
  else { [Convert]::ToBase64String($b) }
}
New-Secret 24 hex   # Pandora DB role password (POSTGRES_PASSWORD)
New-Secret 48       # JWT_SIGNING_KEY
New-Secret 32       # MFA_ENCRYPTION_KEY
```

Save all three in Bitwarden. (`hex` for the DB password avoids `+/=` breaking the
connection string.)

### 2. Provision Pandora's database + role (once per tier)

Run the bootstrap against the tier's Postgres as the superuser, passing the role
password from step 1:

```powershell
Get-Content deploy/bootstrap-pandora.sql | `
  docker exec -i staging-db psql -U postgres -d postgres -v pandora_password='<Pandora DB role secret>'
```

(Or paste `deploy/bootstrap-pandora.sql` into the tier server's Query Tool in
pgAdmin.)

### 3. Create the app env file

```powershell
Copy-Item .env.example .env.staging
# Edit .env.staging:
#   COMPOSE_PROJECT_NAME=pandora-staging  ASPNETCORE_ENVIRONMENT=Production
#   COMPOSE_PROFILES=mailhog  TIER_NETWORK=staging  DB_HOST=staging-db
#   POSTGRES_USER=pandora  POSTGRES_PASSWORD=<Pandora DB role secret>  POSTGRES_DB=pandora
#   JWT_SIGNING_KEY=<secret>  MFA_ENCRYPTION_KEY=<secret>
#   SMTP_HOST=mailhog  SMTP_PORT=1025  MAILHOG_UI_PORT=8732  HTTP_PORT=8731
```

### 4. Register the environment for migrations

```powershell
# First time on this machine only: create the local (gitignored) migris config
Copy-Item migrations/config.example.json migrations/config.json   # skip if it exists
```

Add a `staging` entry under `environments` in `migrations/config.json`:

```json
"staging": {
  "host": "localhost",
  "port": 5433,
  "user": "pandora",
  "password": "<Pandora DB role secret>",
  "database": "pandora"
}
```

### 5. Deploy the app

```powershell
docker compose --env-file .env.staging pull
docker compose --env-file .env.staging up -d
```

### 6. Run migrations

```powershell
cd migrations
migris apply staging -y
cd ..
```

### 7. Verify

```powershell
docker compose --env-file .env.staging ps
docker compose --env-file .env.staging logs -f backend
```

Open the app at `http://<homelab-ip>:8731` and Mailhog at `http://<homelab-ip>:8732`.

**Production deltas:** use the `prod` column above, leave `COMPOSE_PROFILES` empty
(no Mailhog) and point `SMTP_HOST`/`SMTP_PORT` at a real provider, and pin
`BACKEND_IMAGE` / `FRONTEND_IMAGE` to a version tag instead of `:latest`.

### Building images locally (optional)

CI normally builds and pushes the images. To build on the host instead:

```powershell
$env:GITHUB_TOKEN = "<PAT with read:packages>"
docker compose --env-file .env.staging build --secret id=github_token,env=GITHUB_TOKEN backend
docker compose --env-file .env.staging build frontend
```

---

## Migrations

`migris` runs on the homelab host and connects over TCP, which is why the tier DB
publishes a `127.0.0.1` port. Point migris at `127.0.0.1:<DB_PORT>` (staging
`5433`, prod `5432`) with Pandora's role/database (both `pandora`).

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
> tier's shared Postgres (homelab repo). The Postgres volume is deleted only by
> `down -v` on that DB stack, never by Pandora app-stack commands.

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
