# Pandora Documentation

Pandora is a personal modular-monolith .NET backend built on top of the
[`Pottmayer.Tars`](../../tars/docs/README.md) framework. This folder is the functional documentation,
organized by module plus a few cross-cutting concerns.

## How to navigate

- **Before implementing or reviewing code:** read [Project and code standards](architecture/en/coding-standards.md)
  ([pt-BR](architecture/pt-BR/coding-standards.md)) for file placement, layer responsibilities, naming,
  persistence, verification and commits.
- **Before touching the frontend:** read [Frontend architecture](architecture/en/frontend.md)
  ([pt-BR](architecture/pt-BR/frontend.md)) for folders, data, routing, i18n and tests, and
  [Pandora's look](../client-web/DESIGN.md) for themes, tokens and the screen patterns every screen follows.

- **A specific module** ("how does Finances handle a reversal", "what does the Identity API look
  like"): go straight to the module's own `README.md` — it is the index for everything about that
  module.

  | Module | Status | README |
  |---|---|---|
  | [Identity](modules/identity/README.md) | Implemented | Auth, MFA, preferences |
  | [Finances](modules/finances/README.md) | Implemented | Accounts, ledger, imports, recurrences |
  | [Notes](modules/notes/README.md) | Implemented | Spaces, folders, block pages, links, tags, search, history, comments, AI |
  | [Agenda](modules/agenda/README.md) | Implemented | Reminders, tasks, calendar/events |
  | [Channels](modules/channels/README.md) | Implemented | Telegram/email delivery, quiet hours |
  | [Integrations](modules/integrations/README.md) | Implemented (I1–I3, I5); I5 real-Google check pending | OAuth, API-key and password credentials, encrypted at rest; many accounts per provider |
  | [Communications](modules/communications/README.md) | M1–M4 (read, organize, send, rules) done | Email inbox (Gmail, Titan/IMAP): read, organize, send and rules |
  | [Assistant](modules/assistant/README.md) | Implemented (web + Telegram, text + voice, 26 tools) | Natural-language commands over the other modules (Gemini, tools per module) |
  | [Admin](modules/admin/README.md) | Built (A1–A4); browser check pending | Global feature flags, per-action permissions, master admin panel |
  | [Files](modules/files/README.md) | F1 (catalog) and F2 (metadata) implemented; F1 homelab check pending | Catalog of the user's disks, fed by the desktop agent |
  | [Databases](modules/databases/README.md) | Designed (T1–T6), nothing built | User-built bases: tables, typed fields, relations, views, CSV/Excel import and export |
  | [Radar](modules/radar/README.md) | Designed (R1–R5), nothing built | Curated tech digest: followed sources, AI relevance scores, digest through Channels |
  | [Projects](modules/projects/README.md) | Designed (P1–P7), nothing built | Work tracking: custom item types and workflows, backlogs, sprints, boards with nested columns |

- **A cross-cutting decision** that no single module owns:
  - [Messaging architecture](architecture/en/messaging.md) — the in-process outbox, why there is no broker, idempotency, what does *not* go through the bus.
  - [How Pandora is wired to Tars](architecture/en/tars-wiring.md) — every `AddTars*`/`UseTars*` call the backend makes, by family and by file.
  - [Pandora Desktop](architecture/en/desktop-client.md) — the Windows app that wraps `client-web`: shell (implemented, [`client-desktop/`](../client-desktop/README.md)), bridge, desktop modules, device credentials.
  - [Telegram flow — notification and assistant](architecture/en/telegram-assistant-flow.md) — the two end-to-end paths (outbound notification, and inbound message → AI → reply), handler by handler.

- **Deployment**, not module behavior:
  - [Deployment](deployment/deployment.md)
  - [Homelab deploy](deployment/homelab-deploy.md)
  - [Google OAuth and Gemini setup](deployment/google-and-gemini-setup.md) — the OAuth client Gmail needs, the Gemini key the Assistant needs, and how to undo both.

- **[Known issues](known-issues.md)** — understood-but-unfixed cross-cutting problems (the mandatory-configuration
  gate, Finances showing times in the device's zone) and how the fixed time-zone model works.

## Documentation conventions

Every implemented module follows the same shape — see [Finances](modules/finances/README.md) or
[Identity](modules/identity/README.md) as reference examples:

- `modules/<name>/README.md` — the module's own index, English primary, linking to `pt-BR/README.md`.
- `modules/<name>/en/*.md` — topic files: `overview.md`, `architecture.md`, `data-model.md`, one file
  per major capability, `api-reference.md`, `implementation-status.md`.
- `modules/<name>/pt-BR/*.md` — a translation mirror of every `en/` file, same filenames, same section
  structure.
- `product-plan.md` keeps only what is not built. Execution plans and build logs do not stay once the
  work is done.

**Files and Admin are the exceptions**: they do not have the topic set yet — no `overview.md`,
`architecture.md`, `data-model.md`, `api-reference.md` or `implementation-status.md` — only a product
plan (en + pt-BR), which also records what was built. See [Files' README](modules/files/README.md) and
[Admin's README](modules/admin/README.md). **Databases**,
**Radar** and **Projects** are in the same state until they are built: only a product plan
([Databases](modules/databases/README.md), [Radar](modules/radar/README.md),
[Projects](modules/projects/README.md)).

## What is out of scope for this folder

- the Tars framework's own documentation — see [`tars/docs`](../../tars/docs/README.md)
- module-by-module frontend details beyond what a module's `overview.md`/`architecture.md` needs to make sense of the API — the cross-cutting rules are in [Frontend architecture](architecture/en/frontend.md) and [Pandora's look](../client-web/DESIGN.md)
