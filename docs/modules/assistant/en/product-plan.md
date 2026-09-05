# Assistant Module — Product Plan

> **Status:** Plan. The Assistant module backend does not exist yet; the `Tars.Ai` building block
> (Gemini) and storing the key in Integrations **already exist** — see the execution plan.
> 📋 Execution plan (steps): [execution-plan.md](execution-plan.md).
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

Providers are interchangeable behind a port. The current target is **Gemini** (hosted); **OpenAI**
lands as "one more provider" when it earns its place. The idea of a local model (**Ollama**) has been
dropped — the cost/quality tradeoff isn't worth it. The provider is chosen by the user, with the key
stored in Integrations.

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
4. **Providers are ports.** Gemini and OpenAI differ in transport, not in what the module asks of
   them. Switching provider is a settings change and a restart of nothing. In Tars this is already
   keyed DI: `IAiChatCompletionClientFactory.GetClient(provider)`. *(A4)*
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

**Inbound does not run the pipeline inline.** Transcribing and calling the provider takes seconds to
tens of seconds, too long to hold an HTTP caller — and a run that dies mid-way must not become a lost
message. So the subscriber does the cheap part only: it writes a row for the invocation and returns. A
background job in this module picks the row up and runs the seven steps, **one at a time**, which is
where the old broker plan's `prefetch=1` guarantee now lives.

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
| `chat_provider`, `chat_model` | e.g. `gemini` + a fast Gemini model. |
| `transcription_provider`, `transcription_model` | Reserved (A4). May differ from chat. |
| `credential_ref` | Points at the `int001_external_account` row (`auth_kind = api-key`) holding the key. Required for a hosted provider. |
| `endpoint` | Base URL for self-hosted providers. Reserved/null while only Gemini exists. |
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
| `status` | `pending-confirmation` \| `executed` \| `rejected` \| `failed` \| `expired` |
| `result` (jsonb), `error` | |
| `provider`, `model`, `latency_ms`, `tokens_in`, `tokens_out` | Cost and quality tracking. |

### 4.3 Confirmation

`ConfirmationPolicy` on the descriptor, adjusted by the profile's `confirmation_level`:

| Policy | Behaviour |
|---|---|
| `Never` | Execute and report. Read-only commands, and creation of trivially reversible items. |
| `WhenAmbiguous` | Execute unless the model's confidence is low or a required argument was inferred rather than stated. Otherwise echo the parsed intent with **Confirm / Cancel** buttons. |
| `Always` | Never execute without a button press. Deletions, bulk operations, anything financial. |

A pending confirmation is an `ast004` row in `pending-confirmation`, expiring after 10 minutes. The
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

Provider option: since Gemini is already in place, the natural path is **Gemini's own audio input** —
not a self-hosted Whisper (that was the local-arc decision). It may make a separate
`ITranscriptionClient` unnecessary, or make it a thin capability over the same provider. Audio bytes
are transcribed and discarded by default; retention is a per-profile opt-in, because voice is the most
sensitive thing this module touches.

The web surface records with `MediaRecorder`, uploads to
`POST /assistant/interpret` as multipart, and takes the same path.

### 4.5 Prompting

A single system prompt, versioned in source, carrying: the user's current local time and IANA zone,
locale, week start, the names of their calendars and task lists (so "work calendar" resolves), and
the rule that all timestamps must be returned absolute and ISO-8601. Few-shot examples come from the
descriptors' `Examples`, in the user's language — the module must work in Portuguese first, since
that is how it will actually be spoken to.

Conversation history is capped at the last N messages of the active conversation, so we don't pay
tokens reasoning over an unbounded transcript.

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

Namespaced **by capability**, not by provider — so transcription and embeddings land as
`Ai.Transcription.*` / `Ai.Embedding.*` without reorganizing what already exists.

| Project | Contents | State |
|---|---|---|
| `Pottmayer.Tars.Ai.Abstractions` | `AiException` with `IsPermanent` (permanent vs. transient), shared across all capabilities. | **Done** (uncommitted) |
| `Pottmayer.Tars.Ai.Chat.Abstractions` | `IAiChatCompletionClient`, `IAiChatCompletionClientFactory`, and the models `ChatRequest`/`ChatCompletion`/`ChatMessage`/`ToolDefinition`/`ToolCall`/`TokenUsage`. Model and key (`ApiKey`) come **per call**. | **Done** (uncommitted) |
| `Pottmayer.Tars.Ai.Chat` | `KeyedAiChatCompletionClientFactory` + `AddTarsAiClientFactory`. | **Done** (uncommitted) |
| `Pottmayer.Tars.Ai.Chat.Gemini` | `GeminiAiChatCompletionClient` over `v1beta/models/{model}:generateContent`, key in the `x-goog-api-key` header; options `Tars:Ai:Chat:Gemini`. | **Done** (uncommitted, 17 tests green; missing 1st real call) |
| `Pottmayer.Tars.Ai.Chat.OpenAi` | Chat Completions with tools. | Future |
| `Pottmayer.Tars.Ai.Transcription.*` | Audio input (Gemini) / Whisper. | Future (A4) |

Provider selection is per call, not per application: the clients are resolved via keyed DI
(`GetClient(provider)`) using the user's profile, because two users of the same instance may choose
differently.

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
- `Ai.Chat` with chat + tools + the Gemini provider: **already implemented** (uncommitted); missing
  the 1st real call and the commit. See [execution-plan.md](execution-plan.md), Step 0.
- Assistant module shell; `ast001`; register Gemini in the Host; settings UI + reachability test
  (using the user's key in Integrations).
- **Done when:** a settings page can round-trip a prompt through Gemini and show the reply.

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

### Phase A5 — Quality and a second provider
- Full Agenda catalog (`create_event`, `list_agenda`, `complete_task`, `snooze_reminder`) — and, with
  the reads, the decision on personal data leaving the house (see §9.2).
- OpenAI as a second provider (`Ai.Chat.OpenAi` + registration), if there's a real reason beyond Gemini.
- An eval set of real utterances, so switching model is a measured decision rather than a vibe.
- **Done when:** the eval set passes on the chosen model, with the numbers recorded.

### Phase A6 — Beyond Agenda *(future)*
Notes (`create_note`, `search_notes`), Finances (`record_transaction`, `balance_summary`), proactive
digests ("here is your day" every morning at 07:00, generated rather than templated), and
retrieval over Notes for question answering.

---

## 9. Open questions

1. ~~**Where hosted API keys live.**~~ **Decided:** in Integrations, in `int001_external_account`
   with `auth_kind = api-key`. One encrypted store. `ast001`'s `credential_ref` points there, and the
   key is obtained through `IExternalCredentialProvider` — the same synchronous port Agenda uses for
   the Google token. See [Integrations — OAuth & Credentials](../../integrations/en/oauth-and-credentials.md).
2. **Personal data leaves the house.** Since the provider is hosted (Gemini), **every utterance leaves
   the house**, and reads like `list_agenda` would send event titles to Google. Without Ollama this is
   no longer "settle before A5" — it matters **now**. Minimum for the 1st release: a per-profile
   warning, and starting with a *write-only* catalog (`create_reminder`); reads land with an explicit
   decision. See the
   [execution-plan](execution-plan.md#the-question-that-moved-up-personal-data-leaves-the-house).
3. **Streaming.** Not needed for command execution; useful if a conversational mode is ever added. The
   abstraction does not expose it today; deferred.
