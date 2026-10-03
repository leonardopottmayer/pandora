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
                                                   │  6. execute      │  IAssistantTool (each call)
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

public interface IAssistantTool
{
    AssistantCommandDescriptor Descriptor { get; }
    Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement args, CancellationToken ct);
    string Describe(AssistantToolContext context, JsonElement args); // the confirmation question
}

// Who the tool runs for and how to speak to them: every sentence in Locale, every instant in TimeZone.
public sealed record AssistantToolContext(Guid UserId, string Locale, TimeZoneInfo TimeZone);
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
to yesterday. `last_listing` (jsonb) keeps the numbered lines of the last list shown — see §4.6.

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
| `Required` | Like `Always`, but no `confirmation_level` relaxes it — even `trusting` confirms. Deletions (`delete_event`, `delete_task`, `cancel_reminder`). |

A pending confirmation is an `ast004` row in `pending-confirmation`, expiring after 10 minutes. Its
reply is the tool's own `Describe` ("Criar o lembrete \"Pagar o aluguel\" para 05/09/2026 às 10:00?"),
never raw JSON. On Telegram the buttons ride the reply itself — `SendAssistantReply.Buttons`, with
`owner_module: "assistant"` and the invocation id as payload — and Channels registers them in
`chn003_interaction` exactly like a notification's. The tap comes back as `InboundInteractionReceived`
to `AssistantInteractionReceivedHandler`, which runs the same confirm/cancel commands the web uses and
replies with the outcome — the same mechanism Agenda uses, with no code shared between the two.

**Several calls per sentence.** The model may return more than one tool call ("lembra X e cria Y").
Each runs (or is held) on its own and gets its own `ast004` row; the reply numbers them, and the
buttons are numbered to match. The provider cost is one call, recorded on the first row.

**Language.** Replies — the tools' messages, the pipeline's own ("Não entendi…"), the model's prose,
the button labels — follow the profile's locale (`locale_override`, default `pt-BR`), through
`AssistantToolContext`.

Button expiry and single use are guaranteed by `chn003_interaction`; the *invocation's* expiry
(`ast004`) stays this module's business, because that is what decides whether executing still makes
sense.

### 4.4 Voice

**Built (Telegram).** Telegram voice notes are OGG/Opus, but the module need not know that. The flow:

1. Channels publishes `InboundMessageReceived` with `mediaRef` and `mediaMimeType`.
2. The subscriber, seeing no text and an `audio/*` MIME type, reads the bytes through
   `IInboundMediaReader.OpenAsync(channel, bot, mediaRef)` — the only port it calls in Channels. The
   `bot` matters: a Telegram `file_id` is only downloadable by the bot that received it. Notes over
   5 MB are refused with a reply.
3. The interpret pipeline makes a **transcription call** to the same chat provider: the audio travels
   as a `ChatAttachment` on the user turn (Tars `Ai.Chat`, mapped to Gemini `inlineData`), no tools,
   temperature 0. No separate `ITranscriptionClient` was needed.
4. The transcript becomes the utterance and runs the normal text pipeline. Tokens and latency of both
   calls are summed on the invocation; an empty transcript records a clarification under the
   utterance `[voice note]`.
5. The reply echoes what was heard (`🎤 "…"`) above the outcome.

Audio bytes are discarded after transcription. Retention stays a future per-profile opt-in, because
voice is the most sensitive thing this module touches.

The web console records with `MediaRecorder` (up to 150 s), re-encodes the recording in the browser
as 16 kHz mono WAV — browsers record webm/ogg/mp4, and WAV is a format every provider's audio input
accepts — and uploads it to `POST /assistant/interpret/audio` as multipart. From there it takes the
same path as a Telegram voice note (`InterpretInput.Audio`).

### 4.5 Prompting

A single system prompt, versioned in source, carrying: the user's current local time and IANA zone,
locale, week start, the names of their calendars and task lists (so "work calendar" resolves), and
the rule that all timestamps must be returned absolute and ISO-8601. Few-shot examples come from the
descriptors' `Examples`, in the user's language — the module must work in Portuguese first, since
that is how it will actually be spoken to.

Conversation history is capped at the last N messages of the active conversation, so we don't pay
tokens reasoning over an unbounded transcript.

### 4.6 Lists and pointing by number

A read tool that shows items the user may act on (`list_agenda`, `list_tasks`, `search_notes`) numbers
its lines and returns them as `AssistantCommandOutcome.Listed` — kind (`event`, `task`, `reminder`,
`note`), id, title and, for an event occurrence, its start. The pipeline stores them on the conversation
(`ast002.last_listing`, replaced by each new list) and appends to the history recap only each number's
kind (`[numbered for reference: 1=event, 2=task]`), never its title. So "cancela o 2" reaches the model as
"call `delete_task` with `ref: 2`".

Before a call runs or is held for confirmation, the pipeline **pins** the item behind `ref` into the
arguments (`ListedRefs.Pin`: `ref_id`, `ref_kind`, `ref_label`, `ref_at`), dropping any such field the
model sent. What is stored on `ast004` — and later confirmed — is that item, not whatever is number 2 by
the time the user taps *Confirm*. Tools read it through `ToolArguments.OptionalRef`/`PickTarget`, which
refuse a number missing from the last list or one that stands for another kind of item, and otherwise
fall back to the item named by words. Finances lists are bullets, not numbers: nothing in Finances is
changed from the chat.

A call that acts on an existing item (an `IAssistantTargetedTool`) finds it **before** it is held for
confirmation: nothing there (or several) is said at once instead of after *Confirm*, and the item found is
pinned like a listed one, so the question names it as it really is ("Excluir a tarefa \"Organizar fotos
de 2025\"?"). A typed "sim" / "pode" / "não, deixa" to a held call is settled by the pipeline itself,
through the same confirm/cancel commands as the buttons — asked again, the model only repeated the call.

Telegram caps a message at 4096 characters; Channels splits a longer reply at line breaks
(`SendAssistantReplyHandler.Split`), with any buttons on the last piece.

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

### Phase A4 — Voice *(Telegram done 2026-09-22)*
- ✅ Telegram voice notes: media read through `IInboundMediaReader`, transcription via Gemini audio
  input (`ChatAttachment`, Tars 0.0.16), transcript echoed in the reply. See §4.4.
- ✅ Web (2026-09-29): record button in the assistant console; `MediaRecorder` → WAV →
  `POST /assistant/interpret/audio`. See §4.4.
- Pending: audio retention opt-in.
- **Done when:** a voice note in Telegram creates a reminder, in Portuguese.

### Phase A5 — Quality and a second provider
- ✅ Agenda writes (2026-09-27): `create_task`, `create_event`, `complete_task`, `snooze_reminder`. The
  last two take the user's words and find the item locally by title (accent/case-insensitive, word
  prefixes), so no title leaves the house; several matches → the reply lists them and asks which.
  A recurring reminder is snoozed only from its notification button.
- ✅ `list_agenda` (2026-09-29): events, tasks due and reminders for a day or a span (up to 31 days),
  on request. The list is formatted in-house and goes straight to the user; the model only picks the
  tool and the days, and the conversation history keeps a content-free recap (see §9.2).
- OpenAI as a second provider (`Ai.Chat.OpenAi` + registration), if there's a real reason beyond Gemini.
- An eval set of real utterances, so switching model is a measured decision rather than a vibe.
- **Done when:** the eval set passes on the chosen model, with the numbers recorded.

### Phase A6 — Beyond Agenda *(future)*
- ✅ `record_expense` (2026-09-28): "gastei 45 no mercado no Nubank" → a `manual` pending transaction
  in the Finances inbox (never posted directly). The card/account is found locally by the words the
  user used; nothing named and a single account/card → that one; otherwise the reply asks which.
- ✅ `create_note` (2026-09-28): a new top-level page in Notes with the user's text as markdown
  (#tags and [[links]] work as in the editor).
- ✅ `search_notes` (2026-09-29): full-text search over the open notes, up to 5 hits with their
  excerpt, straight to the user; the history keeps a content-free recap (§9.2).
- ✅ Managing things from the chat (2026-10-01), all under §9.2 (data to the user, content-free recap)
  and pointing by number (§4.6):
  - **Agenda** — `list_tasks` (by list, overdue/today/week/undated, or finished; due dates bucketed in
    the user's zone); `update_event` (title, time, place; a new start keeps the length) and
    `delete_event`, both on one occurrence of a series unless the user says "and the following" or
    "all"; `update_task` (title, due date, priority), `reopen_task`, `delete_task`; `cancel_reminder`
    and `rename_reminder` (2026-10-02, over a new `RenameReminderCommand`; on the web since 2026-10-03, the title edits in place in the Reminders list).
    `complete_task` takes a number too, and so does `reschedule_reminder` (2026-10-02, replacing `snooze_reminder`: it moves the remind time itself — a snooze only deferred the alert and every view kept the old time).
  - **Finances, read-only** — `list_transactions` (period, kind, account or card (asked for apart — a card is often named like its account), category with its
    sub-categories, text; latest or largest first; total of all matches), `summarize_transactions`
    (total, by category/account/month/description, against the period before — whole months compare
    with whole months), `account_balances`, `list_cards` (unpaid closed statements, the current one,
    limit left), `list_inbox`. Only posted entries count (not voided, not scheduled). Every number is computed in-house; the model picks the filter only.
    Writes stay as they were — `record_expense` into the inbox; approving, paying and reversing are done
    in the app.
  - Later the same day, after a live run (below): `list_reminders` (pending ones, numbered);
    `update_event` takes a bare `time` ("às 20h" keeps the event's day); `list_agenda` hides acknowledged
    reminders.
  - **Notes** — `read_note` (the whole note, to the user) and `append_to_note` (text added at the end
    after a blank line). Editing the middle of a note would mean sending it to the model, so it is not
    offered.

- ✅ Live run (2026-10-02): `tests/.../Live/AssistantLiveTests` talks to the real Gemini the way Telegram does
  (inbound event → pipeline → Channels, button taps as interactions), on a throwaway database loaded with
  the personal seed, with Telegram captured instead of sent. Opt-in (`PANDORA_LIVE_GEMINI=1`, spends
  tokens); writes a Markdown report (`PANDORA_LIVE_REPORT`). About 8k prompt tokens per call.

Proactive digests ("here is your day" every morning at 07:00 — not for now: `list_agenda` on request
covers it, and under §9.2 it would be templated, not generated).

---

## 9. Open questions

1. ~~**Where hosted API keys live.**~~ **Decided:** in Integrations, in `int001_external_account`
   with `auth_kind = api-key`. One encrypted store. `ast001`'s `credential_ref` points there, and the
   key is obtained through `IExternalCredentialProvider` — the same synchronous port Agenda uses for
   the Google token. See [Integrations — OAuth & Credentials](../../integrations/en/oauth-and-credentials.md).
2. ~~**Personal data leaves the house.**~~ **Decided (2026-09-29):** the utterance still goes to the
   hosted model (Gemini), but the user's data does not. A read tool (`list_agenda`) fetches and formats
   its answer in-house and replies to the user directly; the model only picks the tool and its
   arguments. The history re-sent on the next turn keeps a content-free recap
   (`AssistantCommandOutcome.Recap`) instead of the reply. Since 2026-10-03 that holds for every tool
   reply, writes included: a write names the item it found ("Excluir a tarefa \"X\"?", an ambiguous
   name's matches), so a tool without its own recap is kept as `[command: status; …]`; only the model's
   own words (a clarification) are kept as said. The cost: read replies have a fixed format —
   the model cannot comment on them. A generated digest would need its own decision. See the
   [execution-plan](execution-plan.md#the-question-that-moved-up-personal-data-leaves-the-house).
3. **Streaming.** Not needed for command execution; useful if a conversational mode is ever added. The
   abstraction does not expose it today; deferred.
