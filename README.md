# Pandora

Pandora is a personal system for one person's day-to-day: money, notes, agenda, email, files and an
assistant that works across all of them. It is a self-hosted web app (plus a Windows desktop shell)
backed by a .NET modular monolith, running on a homelab.

It is built on Tars (`Pottmayer.Tars.*`), a set of .NET packages for the plumbing every module needs:
data access, the mediator, messaging, security, AI clients and observability.

## What it does

| Module | What it is for |
|---|---|
| **Finances** | Accounts, cards and statements, a ledger with transfers and installments, bank imports (OFX/CSV), recurring transactions, an inbox to review, a full audit trail |
| **Notes** | A Notion-style knowledge base: spaces, folders, pages made of blocks, links and backlinks, tags, search, graph, version history, comments, templates, daily notes |
| **Agenda** | Reminders, tasks and a calendar with recurring events and alerts |
| **Communications** | An email inbox for Gmail and any IMAP server: read, organize, send, and rules |
| **Assistant** | Natural language (typed or spoken, on the web or Telegram) turned into actions in the other modules, through Gemini |
| **Channels** | How Pandora talks to the user: email and Telegram notifications, quiet hours, the Telegram bots |
| **Integrations** | External accounts (Google OAuth, API keys, passwords), encrypted at rest |
| **Identity** | Sign-up, sign-in, MFA, preferences, paired devices |
| **Admin** | Feature flags, per-action permissions and roles, the admin panel |
| **Files** | A catalog of the user's disks, fed by the desktop agent |

Planned and designed, not built yet: **Databases** (Airtable-style bases), **Projects** (boards and
sprints) and **Radar** (a curated tech digest).

## How it is built

- **Backend:** .NET 10, one process, one module per folder in `backend/src/Modules`. Each module is a
  set of layered projects (Domain, Application, Persistence, Presentation…) with its own PostgreSQL
  schema. Modules talk through small ports and through integration events over an in-process outbox;
  there is no message broker.
- **Database:** PostgreSQL. Migrations are plain `.sql` files per module in `migrations/`, applied with
  migris.
- **Web:** `client-web`, React 19 + TypeScript + Vite + Ant Design 6 + Tailwind + TanStack Query, in
  English and Brazilian Portuguese.
- **Desktop:** `client-desktop`, a WinForms + WebView2 shell around the web app, with modules that do
  what a browser cannot (the Files agent today).
- **Deploy:** Docker images built by GitHub Actions, run with `docker-compose.yml` on the homelab,
  nginx serving the web app and proxying `/api`. One version for everything, in `/VERSION`.

```
backend/          .NET solution (Pottmayer.Pandora.slnx): src/Host, src/Modules, src/Shared, tests
client-web/       the browser app
client-desktop/   the Windows shell and its modules
migrations/       SQL migrations per module, and the local seed
deploy/           homelab deploy files
docs/             module and architecture documentation
```

## Running it locally

You need the .NET 10 SDK, Node.js, PostgreSQL and Docker (for the integration tests).

1. **Tars packages.** They come from GitHub Packages: give the `github` source in
   `backend/nuget.config` a token with `read:packages`.
2. **Database.** Create a PostgreSQL database, copy `migrations/config.example.json` to
   `migrations/config.json` (gitignored) and apply the migrations with migris.
3. **Backend settings** live in `backend/src/Host/Pottmayer.Pandora.Host/appsettings*.json`: the
   connection string, the JWT and MFA keys, and optionally Google OAuth and Telegram. Keep local
   secrets out of commits. [Google OAuth and Gemini setup](docs/deployment/google-and-gemini-setup.md)
   covers the Google side.
4. **Run the backend:**

   ```bash
   dotnet run --project backend/src/Host/Pottmayer.Pandora.Host
   ```

5. **Run the web app** (on http://localhost:5173, calling the backend at `https://localhost:61182`):

   ```bash
   cd client-web && npm install && npm run dev
   ```

**Tests:**

```bash
dotnet test backend/Pottmayer.Pandora.slnx
```

```bash
cd client-web && npm test
```

The integration tests start PostgreSQL in a container (Testcontainers), so Docker must be running, and
the backend must not be running from the same build folder.

## Documentation

Start at the [documentation index](docs/README.md). Every built module has its own folder in
`docs/modules/` with an overview, architecture, data model, API reference and implementation status,
in English and Brazilian Portuguese.

- [Project and code standards](docs/architecture/en/coding-standards.md): read before changing code.
- [Frontend architecture](docs/architecture/en/frontend.md) and [Pandora's look](client-web/DESIGN.md).
- [Messaging](docs/architecture/en/messaging.md) and [how Pandora is wired to Tars](docs/architecture/en/tars-wiring.md).
- [Pandora Desktop](docs/architecture/en/desktop-client.md).
- [Deployment](docs/deployment/deployment.md) and the [homelab deploy](docs/deployment/homelab-deploy.md).
- [Known issues](docs/known-issues.md).
