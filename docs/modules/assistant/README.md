# Assistant Module

> Natural-language command execution against Pandora's own modules, inside the Pandora modular monolith.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).

The **Assistant** module turns natural language — typed or spoken, in the web console or on the
assistant Telegram bot — into commands executed against Pandora's existing modules (Agenda, Notes,
Finances). The model (Gemini) only picks a tool and its arguments; the tool is a thin wrapper over a use
case the owning module already has. It is not a chatbot with opinions and it holds no business rules:
if the assistant can do something the API cannot, that is a bug.

---

## How this documentation is organized

Start with the **Overview** for the principles and vocabulary, then the topic you need.

| # | Document | What it covers |
|---|---|---|
| 1 | [Overview](en/overview.md) | What it does, principles, glossary, scope |
| 2 | [Architecture](en/architecture.md) | Projects, the interpret pipeline, tool catalog, confirmation, privacy, pointing by number, Telegram path, frontend, tests |
| 3 | [Data Model](en/data-model.md) | Schema catalog (`ast001`–`ast004` + outbox): columns, constraints, statuses |
| 4 | [API Reference](en/api-reference.md) | Every endpoint under `/api/v{n}/assistant` |
| 5 | [Implementation Status](en/implementation-status.md) | What is built, what changed from the plan, open points |
| 6 | [Product Plan](en/product-plan.md) | What is still missing (OpenAI, eval set, audio retention, open questions) |
| 7 | [Execution plan](en/execution-plan.md) | Historical: the order the module was built in (Step 0 → A4) |

The end-to-end Telegram sequence is in [Telegram flow](../../architecture/en/telegram-assistant-flow.md).

---

## Quick facts

- **Backend:** `Pottmayer.Pandora.Modules.Assistant.*` (.NET 10, commands/queries over the Tars mediator).
- **Schema:** PostgreSQL schema `assistant`, tables `ast001`–`ast004` + `tars_outbox_message`, PK `uuid_generate_v7()`.
- **Migrations:** `migrations/migrations/assistant/`.
- **API base:** `/api/v{version}/assistant`, authenticated, scoped to the token's user.
- **Frontend:** `client-web/src/modules/assistant` — `/assistant` (chat + history) and `/settings/assistant`.
- **LLM provider:** Gemini via `Tars.Ai.Chat.Gemini`; model and key per call; the key is the user's API-key account in [Integrations](../integrations/README.md) (`ast001.chat_account_id`).
- **Tools:** 25, contributed by [Agenda](../agenda/README.md), [Notes](../notes/README.md) and [Finances](../finances/README.md) as `IAssistantTool`s in their `Application/Assistant` folders; Finances also takes shared files through `IAssistantFileQueue`.
- **Surfaces:** web (inline in the request) and Telegram (integration-event subscribers on [Channels](../channels/README.md) inbound; replies through the Assistant outbox — [Messaging](../../architecture/en/messaging.md)).
