# Assistant Module — Product Plan

> **Status:** Plan. Nothing in this document is implemented yet.
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related plans: [Agenda](../../agenda/en/product-plan.md) ·
> [Channels](../../channels/en/product-plan.md) ·
> [Integrations](../../integrations/en/product-plan.md) ·
> [Messaging](../../../architecture/en/messaging.md)

---

## 1. What the module does

**Assistant** turns natural language — typed or spoken — into commands executed against Pandora's own
modules.

> "lembra de ligar pro dentista amanhã às 9"
> → `create_reminder(title: "Ligar pro dentista", remindAt: 2026-08-16T09:00-03:00)`
> → a real row in `agd006_reminder`, alert scheduled, confirmation sent back in the same chat.

Two surfaces:

- **Telegram** — text messages and **voice notes**. The primary one: it is where the notifications
  already arrive, so the loop closes in one app.
- **Web** — a command bar in the client, plus audio recording, sharing the same pipeline.

Three interchangeable back-ends, chosen by the user: **Ollama** (self-hosted, private, the default
target for the homelab), **OpenAI**, **Gemini**.

### What it is not

It is not a chatbot with opinions about the user's life, and it is not a place where business rules
live. Every action it takes is a command that already exists and that the web UI can also invoke. If
the assistant can do something the API cannot, that is a bug.

---

## 2. Naming and coordinates

| Thing | Value |
|---|---|
| Backend projects | `Pottmayer.Pandora.Modules.Assistant.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}` |
| PostgreSQL schema | `assistant` |
| Table prefix | `astXXX_`, PK `uuid_generate_v7()` |
| API base | `/api/v{version}/assistant` |
| Frontend | `client-web/src/modules/assistant` |
| Migrations | `migrations/migrations/assistant/` |
| Tars building block | `Pottmayer.Tars.Ai.*` (see §6) |

---

## 3. Principles

1. **The LLM chooses; Pandora decides.** The model's only output is a *tool call*: a name and typed
   arguments. Validation, authorization and execution are ordinary application code. A hallucinated
   command fails validation like any bad request. *(A1)*
2. **Modules own their commands.** Each module publishes a command catalog from its `Abstractions`.
   Assistant discovers it at startup and never hard-codes what a reminder is. Adding Finances to the
   assistant is a registration, not a rewrite. *(A2)*
3. **Write actions are confirmed; the threshold is per command.** Creating a reminder from an
   unambiguous sentence executes. Deleting anything, or acting on a low-confidence match, asks first
   — with inline buttons in Telegram. *(A3)*
4. **Providers are ports.** Ollama, OpenAI and Gemini differ in transport, not in what the module
   asks of them. Switching provider is a settings change and a restart of nothing. *(A4)*
5. **Time is given, never guessed.** The model receives the user's current local time, zone and week
   start in its system prompt, and returns absolute ISO timestamps. "Tomorrow at 9" is resolved
   before it reaches a command. *(A5)*
6. **Everything is logged.** Every invocation stores the utterance, the resolved tool call and the
   outcome. This is both the audit trail and the only realistic way to debug a probabilistic
   component. *(A6)*

---

## 4. Architecture

```
 Telegram voice/text ──► Channels ingress ──► inbound.message.telegram
                         (long polling or webhook)          │
                                                            │
 Web command bar / audio ──► POST /assistant/interpret ──────┤
                                                            ▼
                                                   ┌──────────────────┐
                                                   │  Assistant       │
                                                   │  1. transcribe   │  ITranscriptionClient (if audio)
                                                   │  2. build prompt │  time, zone, locale, catalog
                                                   │  3. chat + tools │  IChatCompletionClient
                                                   │  4. validate     │  JSON schema + module validator
                                                   │  5. confirm?     │  policy per command
                                                   │  6. execute      │  IAssistantCommandHandler
                                                   │  7. reply        │  NotifyUserRequested
                                                   └──────────────────┘
                                                            │
                                    Agenda / Notes / Finances command handlers
```

The module **never learns that Telegram exists**. It consumes `InboundMessageReceived(userId,
channel, text?, mediaRef?, mediaMimeType?)` — already normalized by
[Channels](../../channels/en/inbound-and-linking.md) — and fetches media bytes through
the `IInboundMediaReader` port. Swapping to WhatsApp, or coming in from the web, touches nothing
here.

**Inbound does not run the pipeline inline.** Transcribing and running tool-calling on a local model
takes seconds to tens of seconds, which is too long to hold the caller — and a self-hosted Ollama
should not receive concurrent work. So the subscriber does the cheap part only: it writes a row for
the invocation and returns. A background job in this module picks the row up and runs the seven
steps, **one at a time**, which is where the old broker plan's `prefetch=1` guarantee now lives.

That is the module-owned job pattern the rest of Pandora already uses — the same shape as Channels'
dispatcher and Agenda's sweep. It also gets the durability for free: a run that dies mid-way is a row
with a state, not a lost message. See the
[messaging doc §3](../../../architecture/en/messaging.md#3-asynchrony-without-a-broker).

### 4.1 Command catalog

A module contributes descriptors from its `Abstractions` project:

```csharp
public sealed record AssistantCommandDescriptor(
    string Name,                     // "create_reminder"
    string Description,              // shown to the model
    string ParametersJsonSchema,     // the tool schema
    ConfirmationPolicy Confirmation, // Never | WhenAmbiguous | Always
    IReadOnlyList<string> Examples);

public interface IAssistantCommandHandler
{
    string Name { get; }
    Task<AssistantCommandResult> ExecuteAsync(Guid userId, JsonElement args, CancellationToken ct);
}
```

Assistant collects every registered descriptor at startup and renders it as the provider's tool
definitions. Handlers are thin: they map arguments onto the module's existing application command and
send it through the mediator. No business logic is duplicated.

**Agenda's initial catalog** (phase A4, matching Agenda phase 7):
`create_reminder`, `create_task`, `create_event`, `complete_task`, `snooze_reminder`, `list_agenda`
(today/tomorrow/this week), `search_items`.

Later: `create_note` and `search_notes` (Notes), `record_transaction` and `balance_summary`
(Finances).

### 4.2 Schema catalog

**`ast001_assistant_profile`** — per-user configuration.

| Column | Notes |
|---|---|
| `user_id` | Unique. |
| `chat_provider`, `chat_model` | e.g. `ollama` + `llama3.1:8b`, or `openai` + a hosted model. |
| `transcription_provider`, `transcription_model` | May differ from chat — self-hosted Whisper with a hosted chat model is a sensible mix. |
| `credential_ref` | Points at the `int001_external_account` row holding the API key. Null for Ollama. |
| `endpoint` | Base URL for self-hosted providers. |
| `is_enabled`, `locale_override` | |
| `confirmation_level` | `strict` \| `balanced` \| `trusting` — shifts every command's policy one notch. |

**`ast002_conversation`** — `user_id`, `source` (`telegram` \| `web`), `started_at`, `last_message_at`,
`is_active`. A conversation expires after 30 minutes of silence, so "cancel that" cannot reach back
to yesterday.

**`ast003_message`** — `conversation_id`, `role` (`user` \| `assistant` \| `tool`), `content`,
`audio_ref` (nullable), `token_count`, `created_at`.

**`ast004_command_invocation`** — the audit trail.

| Column | Notes |
|---|---|
| `conversation_id`, `message_id` | |
| `command_name`, `arguments` (jsonb) | Exactly what the model asked for. |
| `status` | `pending_confirmation` \| `executed` \| `rejected` \| `failed` \| `expired` |
| `result` (jsonb), `error` | |
| `provider`, `model`, `latency_ms`, `tokens_in`, `tokens_out` | Cost and quality tracking. |

### 4.3 Confirmation

`ConfirmationPolicy` on the descriptor, adjusted by the profile's `confirmation_level`:

| Policy | Behaviour |
|---|---|
| `Never` | Execute and report. Read-only commands, and creation of trivially reversible items. |
| `WhenAmbiguous` | Execute unless the model's confidence is low or a required argument was inferred rather than stated. Otherwise echo the parsed intent with **Confirm / Cancel** buttons. |
| `Always` | Never execute without a button press. Deletions, bulk operations, anything financial. |

A pending confirmation is an `ast004` row in `pending_confirmation`, expiring after 10 minutes. The
buttons are declared on `NotifyUserRequested` with `owner_module: "assistant"`, so the click comes
back on the key `inbound.interaction.assistant.confirm` (or `.cancel`) straight to this module's
subscriber — the same mechanism Agenda uses, with no code shared between the two.

Button expiry and single use are guaranteed by `chn003_interaction`; the *invocation's* expiry
(`ast004`) stays this module's business, because that is what decides whether executing still makes
sense.

### 4.4 Voice

Telegram voice notes are OGG/Opus, but the module need not know that. The flow:

1. Channels publishes `InboundMessageReceived` with `mediaRef` and `mediaMimeType`.
2. Assistant opens the stream through `IInboundMediaReader.OpenAsync(channel, mediaRef)` — the only
   port it calls in Channels.
3. `ITranscriptionClient.TranscribeAsync(stream, mimeType, languageHint)` returns text.
4. From there it is identical to a typed message.

Provider options: self-hosted `whisper.cpp`/`faster-whisper` behind an HTTP endpoint (an
`ITranscriptionClient` implementation, the homelab default), OpenAI's transcription endpoint, or
Gemini's audio input. Audio bytes are transcribed and discarded by default; retention is a
per-profile opt-in, because voice is the most sensitive thing this module touches.

The web surface records with `MediaRecorder`, uploads to
`POST /assistant/interpret` as multipart, and takes the same path.

### 4.5 Prompting

A single system prompt, versioned in source, carrying: the user's current local time and IANA zone,
locale, week start, the names of their calendars and task lists (so "work calendar" resolves), and
the rule that all timestamps must be returned absolute and ISO-8601. Few-shot examples come from the
descriptors' `Examples`, in the user's language — the module must work in Portuguese first, since
that is how it will actually be spoken to.

Conversation history is capped at the last N messages of the active conversation, so a small local
model is not asked to reason over an unbounded transcript.

---

## 5. Failure behaviour

A probabilistic component fails differently from the rest of Pandora, so the failure modes are part
of the design, not an afterthought:

| Situation | Response |
|---|---|
| No tool matched | Reply with what it understood and list the things it can do. Never invent an action. |
| Required argument missing | Ask one targeted question, keeping the partial call in the conversation. |
| Validation rejected the arguments | Report the domain error in plain language; log the raw call. |
| Provider unreachable / timed out | Say so plainly and preserve the utterance for retry. Never silently drop a user's reminder. |
| Model returned malformed JSON | One reprompt, then give up with an honest message. |

The one thing it must never do is claim success it did not achieve — the invocation status is written
from the command result, not from the model's narration.

---

## 6. Tars building block: `Ai`

| Project | Contents |
|---|---|
| `Pottmayer.Tars.Ai.Abstractions` | `IChatCompletionClient` (messages, tool definitions, tool calls, streaming, token usage), `ITranscriptionClient`, `IEmbeddingClient` (reserved), the shared message/tool model, `AiException` with a permanent/transient split. |
| `Pottmayer.Tars.Ai.Ollama` | `/api/chat` with tool support; configurable endpoint and model; no credentials. |
| `Pottmayer.Tars.Ai.OpenAi` | Chat Completions with tools, plus transcription. |
| `Pottmayer.Tars.Ai.Gemini` | `generateContent` with function calling, plus audio input. |

Provider selection is per call, not per application: the clients are resolved from a keyed factory
using the user's profile, because two users of the same instance may choose differently.

Tars gets the transport and the shape; Pandora keeps prompts, catalogs, policies and persistence.
Documentation lands in the Tars repo under `docs/ai/`.

---

## 7. API surface

```
GET    /assistant/profile                POST /assistant/profile          → provider/model settings
GET    /assistant/providers              → available providers, models, reachability probe
POST   /assistant/interpret              → { text } or multipart audio → parsed intent + result
POST   /assistant/invocations/{id}/confirm
POST   /assistant/invocations/{id}/cancel
GET    /assistant/conversations          GET /assistant/conversations/{id}/messages
GET    /assistant/invocations            → the audit trail, filterable by status
GET    /assistant/commands               → the live catalog (debugging, and the web help panel)
```

---

## 8. Roadmap

### Phase A1 — Tars `Ai` + profile
- `Ai.Abstractions` with chat + tools; Ollama implementation; provider/model settings per user with
  a reachability probe.
- `ast001`; settings UI.
- **Done when:** a settings page can round-trip a prompt through a local Ollama and show the reply.

### Phase A2 — Command pipeline (web, text)
- Descriptor registration and discovery; system prompt; tool-call validation; execution through the
  mediator; `ast002`–`ast004`.
- Agenda registers `create_reminder` and `create_task`; the web command bar calls `/interpret`.
- Confirmation flow in the web UI.
- **Done when:** typing "lembrete de pagar o aluguel dia 5 às 10h" in the browser creates the right
  reminder, and the invocation log shows the exact tool call.

### Phase A3 — Chat inbound, text *(depends on [Channels C4 — inbound](../../channels/en/inbound-and-linking.md), already implemented)*
- Subscriber bound to `inbound.message.#`, writing an invocation row drained one at a time; reply through
  `NotifyUserRequested`.
- Confirmation with `owner_module: "assistant"` buttons; subscriber bound to
  `inbound.interaction.assistant.#`.
- **Done when:** the same sentence typed into Telegram does the same thing, and *Confirm* works.

### Phase A4 — Voice
- `ITranscriptionClient`; self-hosted Whisper adapter; media read through `IInboundMediaReader`; web
  `MediaRecorder` upload; audio retention opt-in.
- **Done when:** a voice note in Telegram creates a reminder, in Portuguese.

### Phase A5 — Hosted providers and quality
- OpenAI and Gemini implementations; per-user selection with keys stored encrypted.
- Full Agenda catalog (`create_event`, `list_agenda`, `complete_task`, `snooze_reminder`).
- An eval set of real utterances replayed across providers, so switching model is a measured
  decision rather than a vibe.
- **Done when:** the same eval set passes on Ollama and on a hosted model, with the numbers recorded.

### Phase A6 — Beyond Agenda *(future)*
Notes (`create_note`, `search_notes`), Finances (`record_transaction`, `balance_summary`), proactive
digests ("here is your day" every morning at 07:00, generated rather than templated), and
retrieval over Notes for question answering.

---

## 9. Open questions

1. ~~**Where hosted API keys live.**~~ **Decided:** in Integrations, in `int001_external_account`
   with `auth_kind = api_key`. One encrypted store. `ast001`'s `credential_ref` points there, and the
   key is obtained through `IExternalCredentialProvider` — the same synchronous port Agenda uses for
   the Google token. See [Integrations — OAuth & Credentials](../../integrations/en/oauth-and-credentials.md).
2. **Whether the LLM ever sees personal data.** Reads like `list_agenda` mean event titles go to the
   provider. With Ollama that is local and fine; with OpenAI/Gemini it leaves the house. Minimum: a
   per-profile warning and a switch to restrict hosted providers to *write* commands only. Worth
   settling before A5.
3. **Streaming.** Not needed for command execution; useful if a conversational mode is ever added.
   The abstraction reserves it, the implementation defers it.
4. **Portuguese-first quality on small models.** Local 8B-class models are noticeably weaker at
   Portuguese tool-calling. The eval set in A5 exists to find the smallest model that actually
   works, rather than assuming one.
