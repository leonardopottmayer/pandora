# Assistant Module

> Natural-language command execution against Pandora's own modules, inside the Pandora modular monolith.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).
>
> **Status: plan.** The Assistant module backend is not built yet, but the `Tars.Ai` building block
> (Gemini) and storing the key in Integrations already exist (uncommitted / done) — see
> [product-plan.md](en/product-plan.md) for the full scope and
> [execution-plan.md](en/execution-plan.md) for the current work list (Gemini, hosted).

The **Assistant** module turns natural language — typed or spoken, in Telegram or in the web command
bar — into commands executed against Pandora's existing modules (Agenda, Notes, Finances, ...). It is
not a chatbot with its own opinions or business rules: every action it can take is a command the web
UI can also invoke directly. If the assistant can do something the API cannot, that is a bug.

---

## How this documentation is organized

Unlike the other modules, Assistant does not yet have the full `en/` + `pt-BR/` topic set
(overview, architecture, data-model, api-reference, implementation-status) — there is nothing built to
document. What exists today:

| Document | Language | What it covers |
|---|---|---|
| [Product Plan](en/product-plan.md) / [pt-BR](pt-BR/product-plan.md) | en + pt-BR | Full scope: surfaces (Telegram, Web), providers behind a port (Gemini now, OpenAI later), phases A1–A6 |
| [Execution plan](en/execution-plan.md) / [pt-BR](pt-BR/execution-plan.md) | en + pt-BR | The work list actually being built first: hosted Gemini with the key in Integrations; cuts OpenAI and the Notes/Finances/proactive phases from immediate scope |

Once the module has real implementation, it should move to the same `en/` + `pt-BR/` per-topic
structure the other modules use (see e.g. [Identity](../identity/README.md)), with `overview.md`,
`architecture.md`, `data-model.md`, `api-reference.md` and `implementation-status.md`.

---

## Quick facts

- **Backend:** module not started. Product plan targets a new `Pottmayer.Pandora.Modules.Assistant.*` family. The `Tars.Ai` transport (chat + Gemini) is already implemented (uncommitted).
- **Surfaces:** Telegram (text + voice notes), Web command bar.
- **LLM provider:** Gemini (hosted), behind a keyed-DI port; OpenAI is a later addition. Ollama/local was dropped.
- **First execution slice:** Gemini via `Tars.Ai`, key stored per user in Integrations; first surface is the web command bar, first command is Agenda's `create_reminder`. See [execution-plan.md](en/execution-plan.md).
- **Depends on:** [Messaging](../../architecture/en/messaging.md) (asynchrony without a broker, §3), [Agenda](../agenda/README.md), [Integrations](../integrations/README.md) (the Gemini API key: `ast001.credential_ref` → `int001_external_account`).
