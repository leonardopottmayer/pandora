# Communications Module

> The user's inbox inside the Pandora modular monolith: the messages that arrive *to* the user, from
> any provider, in one place. Email comes first.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).
>
> **Status: M1 (connect and read) implemented; the real-world check is pending.** Gmail, Titan and
> any IMAP server connect, sync headers newest first and open messages in a sandboxed reader.
> Organizing (M2), sending (M3) and rules (M4) are planned.

**Communications** connects any number of mailboxes (Gmail through the Gmail API; Titan Email and any
IMAP/SMTP server through MailKit) and lets the user read, organize and send from Pandora, with rules
that run either as native Gmail filters or in Pandora's own engine. It is the inbound side;
[Channels](../channels/README.md) is the outbound one (Pandora notifying the user). The
planned Radar module (tech news curation) will build on it later.

---

## How this documentation is organized

M1 is built, but the module does not have the usual topic set yet: what was built, and where it
differs from the plan, is recorded step by step in the execution plan's **Built** notes.

| Document | Language | What it covers |
|---|---|---|
| [Product Plan](en/product-plan.md) / [pt-BR](pt-BR/product-plan.md) | en + pt-BR | Email in full: providers, credentials, data model, sync, organizing, sending, rules (with two-way Gmail filters), screens, API, errors, testing, phases M1–M4 |
| [Execution plan](en/execution-plan.md) / [pt-BR](pt-BR/execution-plan.md) | en + pt-BR | M1 (connect and read), step by step, with what each step built |

The usual `en/` + `pt-BR/` topic set (`overview.md`, `architecture.md`, `data-model.md`,
`api-reference.md`, `implementation-status.md`) comes once M1 passes its real-world check.

---

## Quick facts

- **Backend:** `Pottmayer.Pandora.Modules.Communications.*` (7 projects). IMAP through MailKit, Gmail
  through its REST API (no Google SDK), message bodies parsed with MimeKit.
- **Schema:** PostgreSQL schema `communications`, tables `com001_mailbox`…`com005_mail_content`
  (`com006_mail_rule` is M4's), migrations in `migrations/migrations/communications/`.
- **Sync:** a background job runs one cycle per mailbox every 2 minutes (labels, changes, one backfill
  batch); "Sync now" runs one on demand. `MailMessageReceived` goes out through the outbox.
- **API base:** `/api/v1/communications` — mailboxes, labels, messages, attachments.
- **Credentials:** in Integrations. Gmail is an OAuth account; Titan/IMAP is a `password` account.
  Each mailbox stores the account id it runs on.
- **Storage:** headers, labels and snippet are mirrored; bodies and attachments are fetched on demand
  and cached.
- **Frontend:** `mail` and `mail/accounts` in `client-web/src/modules/communications` (`mail/rules`
  is M4's). HTML bodies are sanitized by `lib/prepareMailHtml` and shown in a sandboxed iframe with a
  CSP; remote images stay blocked until "Show images".
