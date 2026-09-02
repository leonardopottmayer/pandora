# Assistant Module

> Natural-language command execution against Pandora's own modules, inside the Pandora modular monolith.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).
>
> **Status: plan only.** Nothing described in this module's docs is implemented yet — see
> [product-plan.md](en/product-plan.md) for the full scope and
> [local-first-plan.md](pt-BR/local-first-plan.md) for the current execution slice (Ollama, local-first).

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
| [Product Plan](en/product-plan.md) / [pt-BR](pt-BR/product-plan.md) | en + pt-BR | Full scope: surfaces (Telegram, Web), the three interchangeable LLM back-ends (Ollama, OpenAI, Gemini), phases A1–A6 |
| [Local-first implementation plan](en/local-first-plan.md) / [pt-BR](pt-BR/local-first-plan.md) | en + pt-BR | The execution slice actually being built first: Ollama-only, self-hosted, cuts OpenAI/Gemini and the Notes/Finances/proactive phases from immediate scope |

Once the module has real implementation, it should move to the same `en/` + `pt-BR/` per-topic
structure the other modules use (see e.g. [Identity](../identity/README.md)), with `overview.md`,
`architecture.md`, `data-model.md`, `api-reference.md` and `implementation-status.md`.

---

## Quick facts

- **Backend:** not started. Product plan targets a new `Pottmayer.Pandora.Modules.Assistant.*` family.
- **Surfaces:** Telegram (text + voice notes), Web command bar.
- **LLM back-ends:** Ollama (self-hosted, local-first target), OpenAI, Gemini — chosen per user.
- **First execution slice:** Ollama only, `qwen2.5:7b-instruct`, deployed as a `docker-compose` service; first command is Agenda's `create_reminder`. See [local-first-plan.md](pt-BR/local-first-plan.md).
- **Depends on:** [Messaging](../../architecture/en/messaging.md) (asynchrony without a broker, §3), [Agenda](../agenda/README.md), [Integrations](../integrations/README.md) (deferred in the local-first slice — no `ast001.credential_ref` needed for Ollama).
