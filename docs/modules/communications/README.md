# Communications Module

> The user's inbox inside the Pandora modular monolith: the messages that arrive *to* the user, from
> any provider, in one place. Email comes first.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).
>
> **Status: plan.** The backend projects exist as an empty template (wired into the Host, no
> features, no migrations). Its prerequisite, multiple accounts per provider in
> [Integrations](../integrations/README.md) (phase I5), is built.

**Communications** connects any number of mailboxes (Gmail through the Gmail API; Titan Email and any
IMAP/SMTP server through MailKit) and lets the user read, organize and send from Pandora, with rules
that run either as native Gmail filters or in Pandora's own engine. It is the inbound side;
[Channels](../channels/README.md) is the outbound one (Pandora notifying the user). The
planned Radar module (tech news curation) will build on it later.

---

## How this documentation is organized

Like [Assistant](../assistant/README.md) before it was built, Communications has only its plans so
far:

| Document | Language | What it covers |
|---|---|---|
| [Product Plan](en/product-plan.md) / [pt-BR](pt-BR/product-plan.md) | en + pt-BR | Email in full: providers, credentials, data model, sync, organizing, sending, rules (with two-way Gmail filters), screens, API, errors, testing, phases M1–M4 |
| [Execution plan](en/execution-plan.md) / [pt-BR](pt-BR/execution-plan.md) | en + pt-BR | M1 (connect and read), step by step |

Once M1 is built, the module moves to the usual `en/` + `pt-BR/` topic set (`overview.md`,
`architecture.md`, `data-model.md`, `api-reference.md`, `implementation-status.md`).

---

## Quick facts

- **Backend:** `Pottmayer.Pandora.Modules.Communications.*` (7 projects, empty template).
- **Schema (planned):** PostgreSQL schema `communications`, tables `com001_mailbox`…`com006_mail_rule`.
- **Credentials:** in Integrations. Gmail is an OAuth account; Titan/IMAP is a `password` account.
  Each mailbox stores the account id it runs on.
- **Storage:** headers, labels and snippet are mirrored; bodies and attachments are fetched on demand
  and cached.
- **Frontend (planned):** `mail`, `mail/accounts` and `mail/rules` in `client-web`.
