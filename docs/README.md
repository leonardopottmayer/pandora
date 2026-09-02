# Pandora Documentation

Pandora is a personal modular-monolith .NET backend built on top of the
[`Pottmayer.Tars`](../../tars/docs/README.md) framework. This folder is the functional documentation,
organized by module plus a few cross-cutting concerns.

## How to navigate

- **A specific module** ("how does Finances handle a reversal", "what does the Identity API look
  like"): go straight to the module's own `README.md` — it is the index for everything about that
  module.

  | Module | Status | README |
  |---|---|---|
  | [Identity](modules/identity/README.md) | Implemented | Auth, MFA, preferences |
  | [Finances](modules/finances/README.md) | Implemented | Accounts, ledger, imports, recurrences |
  | [Notes](modules/notes/README.md) | Implemented | Pages, wikilinks, tags, search |
  | [Agenda](modules/agenda/README.md) | Implemented | Reminders, tasks, calendar/events |
  | [Channels](modules/channels/README.md) | Implemented | Telegram/email delivery, quiet hours |
  | [Integrations](modules/integrations/README.md) | Implemented (I1+I2) | OAuth credentials, encrypted at rest |
  | [Assistant](modules/assistant/README.md) | **Plan only** | Natural-language commands over the other modules |

- **A cross-cutting decision** that no single module owns:
  - [Messaging architecture](architecture/en/messaging.md) — the in-process outbox, why there is no broker, idempotency, what does *not* go through the bus.
  - [How Pandora is wired to Tars](architecture/en/tars-wiring.md) — every `AddTars*`/`UseTars*` call the backend makes, by family and by file.

- **Deployment**, not module behavior:
  - [Deployment](deployment/deployment.md)
  - [Homelab deploy](deployment/homelab-deploy.md)

## Documentation conventions

Every implemented module follows the same shape — see [Finances](modules/finances/README.md) or
[Identity](modules/identity/README.md) as reference examples:

- `modules/<name>/README.md` — the module's own index, English primary, linking to `pt-BR/README.md`.
- `modules/<name>/en/*.md` — topic files: `overview.md`, `architecture.md`, `data-model.md`, one file
  per major capability, `api-reference.md`, `implementation-status.md`.
- `modules/<name>/pt-BR/*.md` — a translation mirror of every `en/` file, same filenames, same section
  structure.

**Assistant is the one exception**: it has no implementation yet, so it has no `overview.md`,
`architecture.md`, `data-model.md`, `api-reference.md` or `implementation-status.md` — only a product
plan (en + pt-BR) and a pt-BR-only local-first execution plan. See its
[README](modules/assistant/README.md) for the specifics and the known gap (no English mirror yet for
the local-first plan).

## What is out of scope for this folder

- the Tars framework's own documentation — see [`tars/docs`](../../tars/docs/README.md)
- frontend implementation details beyond what a module's `overview.md`/`architecture.md` needs to make sense of the API — see [`client-web/README.md`](../client-web/README.md)
