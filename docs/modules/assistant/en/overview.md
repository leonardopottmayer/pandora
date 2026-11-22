# Overview — Business & Principles

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

---

## 1. What the module does

The **Assistant** module turns natural language — typed or spoken — into commands executed against
Pandora's own modules:

> "lembra de ligar pro dentista amanhã às 9"
> → `create_reminder({ "title": "Ligar pro dentista", "remindAt": "2026-08-16T09:00:00-03:00" })`
> → a real reminder in Agenda, and a confirmation in the same chat.

It lets a single user:

- Talk to Pandora from the **web console** (`/assistant`, text or a recorded voice note) or from the
  **assistant Telegram bot** (text or voice notes).
- **Create and manage** Agenda items (reminders, tasks, events), **write and read** Notes, **record an
  expense** into the Finances inbox and **ask about** their money (transactions, totals, balances,
  cards, inbox) — 25 tools today, see [Architecture §3](architecture.md#3-the-tool-catalog).
- Point at a line of the last list by number ("cancela o 2").
- Confirm or cancel a held call with buttons (web, Telegram) or a typed "sim" / "não".
- **Share a file** (receipt, boleto) with the Telegram bot; its caption sends it to a module's queue
  (Finances today) without interpreting it.
- Inspect every interpretation afterwards in the **history** tab: the exact tool call, its outcome,
  model, latency and tokens.

It is not a chatbot with opinions and it holds no business rules: every action is a use case the
owning module already exposes. If the assistant can do something the API cannot, that is a bug.

## 2. Core principles

1. **The LLM chooses; Pandora decides.** The model's only useful output is a tool call (name + JSON
   arguments). Reading the arguments, finding the item and running the use case is ordinary code; a
   hallucinated call fails like any bad request.
2. **Modules own their tools.** Each module registers `IAssistantTool` implementations from its own
   `Application/Assistant` folder. The Assistant discovers them through DI and never knows what a
   reminder is.
3. **Writes are confirmed by policy.** Each tool declares a `ConfirmationPolicy`; the user's
   `confirmation_level` shifts it one notch, except `Required` (deletions), which always confirms.
4. **Time is given, never guessed.** The system prompt carries the user's local time, IANA zone and
   week start; the model returns absolute ISO-8601 timestamps.
5. **Personal data stays home.** The utterance goes to the hosted model; the user's data does not. Read
   tools format their answer in-house and send it to the user directly; the history re-sent to the
   model keeps only a content-free recap. Items named by words are matched locally.
6. **Never claim a success that did not happen.** The invocation status comes from the use case's real
   result, not from the model's prose.
7. **Everything is logged.** Every utterance leaves at least one `ast004` row — including the ones that
   did nothing.

## 3. Ubiquitous language (glossary)

| Term | Meaning |
|---|---|
| **Profile** | The user's assistant configuration: provider, the Integrations API-key account it spends, model, on/off, locale override, confirmation level (`ast001`). |
| **Utterance** | What the user said: the typed text, or the transcript of a voice note. |
| **Tool** | A command a module contributes (`IAssistantTool`): a descriptor shown to the model plus `ExecuteAsync` and `Describe`. |
| **Descriptor** | `AssistantCommandDescriptor`: name, description, JSON schema of the arguments, confirmation policy, few-shot examples. |
| **Tool call** | The model's choice: a tool name and its arguments. A sentence can produce several. |
| **Invocation** | One recorded tool call (or the lack of one) and how it ended (`ast004`). The audit trail. |
| **Held call** | An invocation in `pending-confirmation`: valid, waiting for Confirm/Cancel for up to 10 minutes. |
| **Conversation** | A short thread of turns (`ast002`), lapsing after 30 minutes of silence; shared by web and Telegram. |
| **Recap** | What the history keeps of a reply, so the next turn has context without the user's data. |
| **Listing** | The numbered lines of the last list shown (`ast002.last_listing`), so "o 2" can be resolved. |
| **Pinning** | Writing the item a number (or a name) stands for into the arguments before a call runs or is held (`ListedRefs`). |
| **File queue** | A module's inbox for files shared with the bot (`IAssistantFileQueue`), picked by caption keywords. |

## 4. Scope

### In scope (implemented — see [Implementation Status](implementation-status.md))

Profile + provider test, the interpret pipeline (text and voice, multi-turn, several calls per
sentence), confirmation policies and levels, pointing by number, the 25-tool catalog across Agenda,
Notes and Finances, the Telegram path (messages, voice, confirmation buttons, shared files) with
replies through the outbox, the web console and settings page.

### Out of scope / future

| Feature | Status |
|---|---|
| **OpenAI (second provider)** | Not built — one `Ai.Chat.OpenAi` client + one catalog entry. |
| **Eval set of real utterances** | Not built; the opt-in live test is the closest thing. |
| **Audio retention opt-in** | Not built — audio is always discarded after transcription. |
| **Proactive digests** | Not planned for now; `list_agenda` on request covers it. |
| **Changing Finances from the chat** | Deliberately out: only `record_expense` (into the inbox) writes. |

See [Product plan](product-plan.md) for what remains.
