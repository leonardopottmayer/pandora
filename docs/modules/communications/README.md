# Communications Module

> The user's inbox inside the Pandora modular monolith: the messages that arrive *to* the user, from
> any provider, in one place. Email comes first.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).
>
> **Status: M1 (connect and read) and M2 (organize) done, checked against real accounts.** Gmail, Titan and any IMAP server connect, sync
> headers newest first, open messages in a sandboxed reader, and organize mail both ways. Sending (M3) and rules (M4) are planned.

**Communications** connects any number of mailboxes (Gmail through the Gmail API; Titan Email and any
IMAP/SMTP server through MailKit) and lets the user read, organize and send from Pandora, with rules
that run either as native Gmail filters or in Pandora's own engine. It is the inbound side;
[Channels](../channels/README.md) is the outbound one (Pandora notifying the user). The
planned Radar module (tech news curation) will build on it later.

---

## How this documentation is organized

Start with the **Overview** for the boundary and vocabulary, then read the topic you need.

| # | Document | What it covers |
|---|---|---|
| 1 | [Overview](en/overview.md) | What the module does and does not do, principles, glossary, scope |
| 2 | [Architecture](en/architecture.md) | Projects, the provider port and adapters, the sync cycle, failures, the mail reader's security, decisions |
| 3 | [Data Model](en/data-model.md) | `com001`–`com005`, and the content cache in real numbers |
| 4 | [API Reference](en/api-reference.md) | Endpoints, payloads, errors, the event |
| 5 | [Implementation Status](en/implementation-status.md) | What is built, the real-world check, deviations from the plan, open points |
| 6 | [Product Plan](en/product-plan.md) | What is left: send (M3), rules (M4) |
| 7 | [Execution Plan](en/execution-plan.md) | How M1 was built, step by step (history) |
| 8 | [M2 Execution Plan](en/execution-plan-m2.md) | How M2 was built, step by step (history) |
| 9 | [M3 Execution Plan](en/execution-plan-m3.md) | The next phase, send: decisions and steps (plan) |

🇧🇷 Every document also exists in Portuguese, under [`pt-BR/`](pt-BR/README.md).

---

## Quick facts

- **Backend:** `Pottmayer.Pandora.Modules.Communications.*` (7 projects). IMAP through MailKit, Gmail
  through its REST API (no Google SDK), message bodies parsed with MimeKit.
- **Schema:** PostgreSQL schema `communications`, tables `com001_mailbox`…`com005_mail_content`
  (`com006_mail_rule` is M4's), migrations in `migrations/migrations/communications/`.
- **Sync:** a background job runs one cycle per mailbox every 2 minutes (labels, changes, backfill batches
  within a 5-minute budget); "Sync now" runs one on demand. `MailMessageReceived` goes out through the outbox.
- **API base:** `/api/v1/communications` — mailboxes, labels, messages, attachments.
- **Credentials:** in Integrations. Gmail is an OAuth account; Titan/IMAP is a `password` account.
  Each mailbox stores the account id it runs on.
- **Storage:** headers, labels and snippet are mirrored; bodies and attachments are fetched on demand
  and cached.
- **Frontend:** `mail` and `mail/accounts` in `client-web/src/modules/communications` (`mail/rules`
  is M4's). HTML bodies are sanitized by `lib/prepareMailHtml` and shown in a sandboxed iframe with a
  CSP; remote images stay blocked until "Show images".
