# Deployment (containers)

Operational runbook for building and running Pandora with Docker on the homelab.
For the roadmap and rationale, see [homelab-deploy.md](homelab-deploy.md).

Everything here is reproducible from a clean checkout — follow
[From zero to running](#from-zero-to-running) top to bottom.

---

## Architecture

`docker-compose.yml` defines one self-contained stack per environment:

| Service | Image | Role |
|---|---|---|
| `postgres` | `postgres:17` | Database. Data in the named volume `pandora_pgdata` (survives `down`). Port published on `127.0.0.1` only. |
| `backend` | `pandora-backend` (GHCR) | .NET API, HTTP on `8080` **inside** the Docker network (no host port). |
| `frontend` | `pandora-frontend` (GHCR) | nginx: serves the built SPA and proxies `/api` -> `backend:8080` (same origin, no CORS). The only service with a LAN-visible port. |

The frontend is built with an empty `VITE_API_URL`, so the browser talks to the
same origin and nginx forwards `/api` to the backend.

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
| `docker-compose.yml` | The stack. Project name, ports, secrets all come from an env file. |
| `.env.example` | Template for the per-environment env files. |
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
openssl rand -base64 48   # JWT_SIGNING_KEY
openssl rand -base64 32   # MFA_ENCRYPTION_KEY
```

| Variable | What it does |
|---|---|
| `COMPOSE_PROJECT_NAME` | Isolates containers/network/volumes between environments. |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment (`Production` for real deploys). |
| `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` | Postgres + all three connection strings. |
| `POSTGRES_PORT` | Localhost-only port for host tools (migris). Unique per env. |
| `JWT_SIGNING_KEY` | Overrides `Tars:Security:Identity:Jwt:SigningKey`. |
| `MFA_ENCRYPTION_KEY` | Overrides `Pandora:Identity:Mfa:EncryptionKey`. |
| `BACKEND_IMAGE` / `FRONTEND_IMAGE` | Which image tag to run (prod: pinned; staging: `:latest`). |
| `HTTP_PORT` | The LAN-visible frontend port. Unique per env. |

Keep a copy of each env file in Bitwarden. **Never** commit real secrets.

---

## Versioning

`VERSION` at the repo root is the single source of truth;
`backend/Directory.Build.props` injects it into every backend project. Bump it
before cutting a release, so the CI publishes images tagged with the new version.

---

## Environments & ports

Multiple environments share the **same images** and the **same compose file**;
only the env file differs. Each is isolated by its Compose project name.

| Environment | `COMPOSE_PROJECT_NAME` | `HTTP_PORT` | `POSTGRES_PORT` | Image tag | Data |
|---|---|---|---|---|---|
| Production | `pandora-prod` | `8730` | `5432` | pinned (e.g. `:0.1.0`) | real, backed up |
| Staging | `pandora-staging` | `8731` | `5433` | `:latest` | disposable |

`HTTP_PORT` is the only LAN-visible port. `POSTGRES_PORT` is bound to `127.0.0.1`
(host tools only). In phase 2, access moves to a hostname via Cloudflare Tunnel.

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

On the homelab, from a clean checkout:

```bash
# 0. One-time: docker login ghcr.io (see setup) and create .env.prod

# 1. Pull the images built by CI
docker compose --env-file .env.prod pull

# 2. Start the stack (postgres waits healthy before backend starts)
docker compose --env-file .env.prod up -d

# 3. Apply database migrations (see "Migrations" below)
migris apply prod

# 4. Open the app on the LAN
#    http://<homelab-ip>:8730
```

Building locally instead of pulling (optional):

```bash
export GITHUB_TOKEN=<PAT with read:packages>
docker compose --env-file .env.prod \
  build --secret id=github_token,env=GITHUB_TOKEN backend
docker compose --env-file .env.prod build frontend
```

---

## Migrations

`migris` runs on the homelab host and connects over TCP, which is why Postgres
publishes a `127.0.0.1` port. Point migris at `127.0.0.1:${POSTGRES_PORT}` with
the environment's `POSTGRES_*` credentials and database.

> The prod/staging DB credentials are secrets. **Do not** add them to the
> committed `migrations/config.json` (public repo) — keep the prod/staging
> connection in a local, untracked migris config on the homelab. The committed
> `config.json` should only carry the throwaway `local` dev entry.

Run migrations after `up -d`, and again whenever a deploy includes new migration
files.

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

> The Postgres volume is deleted only with an explicit
> `docker compose --env-file .env.prod down -v`. Plain `down` keeps the data.

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
