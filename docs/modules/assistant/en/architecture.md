# Architecture

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Overview](overview.md)

---

## 1. Project layout

The module follows the same layered split as the others, under `backend/src/Modules/Assistant/`:

```
Pottmayer.Pandora.Modules.Assistant.
  Abstractions      → the public contract other modules implement: IAssistantTool, IAssistantTargetedTool,
                      AssistantCommandDescriptor, ConfirmationPolicy, ListedRefs, ToolArguments,
                      IAssistantFileQueue, SharedFile; AssistantModule, AssistantOptions
  Application       → Commands (Interpret, Confirm/CancelInvocation, SaveProfile, TestProvider), Queries
                      (GetProfile, GetProviders, GetInvocations), Interpret/ (system prompt, context,
                      yes/no parser), Subscribers/ (Telegram inbound + button taps), DI
  Contracts         → empty — the module publishes Channels' SendAssistantReply, not events of its own
  Domain            → Aggregates (AssistantProfile, Conversation, Message, CommandInvocation),
                      ValueObjects (ConfirmationLevel, InvocationStatus, MessageAuthor), Errors, Ports
  Infrastructure    → DI only: binds AssistantOptions and registers the Tars.Ai Gemini chat client
  Persistence       → AssistantDbContext (+ Tars outbox mapping), EntityConfigs, Repositories, DI
  Presentation      → AssistantController, DI
```

Host wiring (`Program.cs`): `AddAssistantPersistence()`, `AddAssistantInfrastructure()`,
`AddAssistantApplication()`, `AddAssistantPresentationPart()`; `OutboxRegistration.cs` adds
`AddTarsOutboxRelay(AssistantModule.DatabaseKey)`. See [Tars wiring](../../../architecture/en/tars-wiring.md).

**Provider.** Gemini through `Pottmayer.Tars.Ai.Chat.Gemini`, resolved per call with
`IAiChatCompletionClientFactory.GetClient(profile.ChatProvider)`. No key is configured in the Host:
the key is read per call from Integrations (`IExternalCredentialProvider.GetApiKeyAsync(userId,
accountId)`) and passed as `ChatRequest.ApiKey`. Model per call too (`ChatRequest.Model`), default
`gemini-3.6-flash` (`AssistantDefaults`). Temperature 0 everywhere.

**Text generation for other modules.** `ITextCompletion` in `Assistant.Abstractions/Text` is a
synchronous port registered in Application. `TextCompletion` sends `CompleteTextCommand`, requiring
`assistant.commands.run` and the module feature. It reuses the user's enabled profile, model and key;
no tools, history or prompt/invocation writes. Notes uses it for B9 writing previews, sending only
selected text or bounded preceding context. Provider failures and empty or oversized (over 16000
characters) responses return `Assistant.TextCompletionFailed` without provider details. This is separate
from the interpret pipeline's read tools below, which still never resend notes to the model.

## 2. The interpret pipeline

`InterpretCommandHandler` is the core; the web endpoints and the Telegram subscriber both send
`InterpretCommand`.

1. **Pre-flight.** Empty input → `Assistant.EmptyText`. No profile / disabled → `Assistant.NotEnabled`.
   No chat account, or its key cannot be read → `Assistant.NoApiKey`. These are the only failures
   returned as errors; everything after records an invocation.
2. **Conversation.** The given `conversationId`, else the user's most recent one; a fresh one when it
   is missing, someone else's, or idle for more than 30 minutes (`Conversation.IdleTimeout`). No
   conversation id → the thread is shared between web and Telegram.
3. **Context.** Locale (`locale_override`, default `pt-BR`), effective time zone
   (`IEffectiveTimeZoneResolver`, which falls back to the account default) and week start
   (`IUserPreferencesReader`) → an `AssistantToolContext`.
4. **Voice.** An audio input (max 5 MB) is transcribed first by its own call to the same model — the
   audio rides as a `ChatAttachment` on the user turn, no tools. The transcript becomes the utterance;
   an empty one records a clarification under `[voice note]`. The audio is not kept.
5. **Typed yes/no.** In an existing conversation, a reply made only of yes/no words (`ConfirmationReply`:
   "sim", "pode", "não, deixa", up to 4 words) settles every call held there through the same
   Confirm/Cancel commands — the model is not asked.
6. **Prompt.** `AssistantSystemPrompt` (versioned, `Version = 6`): role, local now + weekday, IANA zone,
   week start, locale, the absolute-ISO-8601 rule, "ask one question instead of inventing", how to
   pass `ref`, and every descriptor with its examples. Tools go as `ToolDefinition`s.
7. **History.** Up to `Pandora:Assistant:HistoryMessageLimit` (default 10) recent `ast003` messages of
   the active conversation, between the system prompt and the utterance. If the model answers by
   imitating a recap line (`[tool: …]`), the call is retried once without the history.
8. **Outcome per tool call.** No tool call → `clarification` with the model's prose (or "Não entendi").
   Otherwise each call, in order, through `RunAsync`:
   - unknown tool → `rejected`; a `ref` number not on the last list → `rejected`;
   - `ListedRefs.Pin` writes the listed item into the arguments;
   - if confirmation is required (§4): an `IAssistantTargetedTool` finds its item first (none / several
     → `failed` at once; one → pinned with `PinTarget`), then the call is held as
     `pending-confirmation` with the tool's `Describe` as the question, expiring in 10 minutes;
   - otherwise `ExecuteAsync` → `executed` or `failed` with the tool's message;
   - `ArgumentException` / `FormatException` / `InvalidOperationException` → `rejected`.
   A provider failure (`AiException`) at any call → `provider-error`.
9. **Record.** One unit of work: touch/insert the conversation (and its `last_listing` when a step
   returned one), the user message, the assistant message (the **recap**, §5), one `ast004` row per step.
   Latency and tokens (transcription included) go on the first row only. Several steps → the reply is
   numbered.

The result (`InterpretResultDto`) carries the conversation id, the combined reply, one entry per
invocation (id, status, tool name, arguments, message) and the transcript.

## 3. The tool catalog

A module contributes tools by registering `IAssistantTool` implementations in its `ApplicationDI`
(`services.AddScoped<IAssistantTool, …>()`); the Assistant takes `IEnumerable<IAssistantTool>`. A tool
is thin: it reads its arguments with `ToolArguments`, finds the item locally (`MatchByTitle` /
`PickByTitle` / `PickTarget`: accent- and case-insensitive, word prefixes, ignoring words under three
letters) and sends the module's existing command or query through the mediator. Its `Permissions` list
the permissions those use cases require: `Interpret` offers the model only the tools whose permissions
the user holds (and whose features are on), and the use cases still check them when the tool runs.

| Module | Tool | Policy | Notes |
|---|---|---|---|
| Agenda | `create_reminder`, `create_task`, `create_event` | WhenAmbiguous | optional recurrence; task list / calendar by name; a task may go under a parent |
| Agenda | `complete_task`, `reopen_task`, `update_task` | WhenAmbiguous | targeted |
| Agenda | `update_event` | WhenAmbiguous | targeted; one occurrence unless "and the following" / "all" |
| Agenda | `reschedule_reminder`, `rename_reminder` | WhenAmbiguous | targeted |
| Agenda | `delete_task`, `delete_event`, `cancel_reminder` | **Required** | targeted |
| Agenda | `list_agenda`, `list_tasks`, `list_reminders` | Never | numbered list |
| Notes | `create_note` | WhenAmbiguous | new top-level page |
| Notes | `append_to_note` | WhenAmbiguous | targeted; text added at the end |
| Notes | `search_notes` | Never | numbered list |
| Notes | `ask_notes` | Never | an answer from the notes, with the cited notes numbered |
| Notes | `read_note` | Never | the whole note, to the user |
| Finances | `record_expense` | WhenAmbiguous | a `manual` pending transaction in the inbox, never posted |
| Finances | `list_transactions`, `summarize_transactions`, `account_balances`, `list_cards`, `list_inbox` | Never | read-only, bullets (not numbered) |

Source: `backend/src/Modules/{Agenda,Notes,Finances}/…Application/Assistant/`. Finances changes
nothing from the chat beyond `record_expense`.

**File queues.** `IAssistantFileQueue` (name, keywords, `EnqueueAsync(SharedFile)`) is the other
contribution point. Finances registers `FinancesFileQueue` (keywords such as `comprovante`, `boleto`,
`financeiro`, `fatura`); a matching file lands unassigned in `fin017_attachment` with the caption as
its note.

## 4. Confirmation

`ConfirmationPolicy` (`Never`, `WhenAmbiguous`, `Always`, `Required`) is shifted by the profile's
`ConfirmationLevel`, and a call is held when the shifted policy is `Always`, or the declared one is
`Required`:

| Level | Never → | WhenAmbiguous → | Always → | Held in practice |
|---|---|---|---|---|
| `strict` | WhenAmbiguous | Always | Always | every write; reads run |
| `balanced` (default) | Never | WhenAmbiguous | Always | `Always` and `Required` tools |
| `trusting` | Never | Never | WhenAmbiguous | only `Required` tools |

`WhenAmbiguous` has no ambiguity detector: when not shifted to `Always` it runs. Ambiguity is handled
by the model asking a question (a `clarification`) and by targeted tools refusing a name that matches
zero or several items. No tool declares `Always` today, so with `balanced` only deletions are held.

A held call is settled by `POST …/invocations/{id}/confirm|cancel`, a Telegram button, or a typed
yes/no. Confirm re-runs the **stored** arguments (pinned item included) and writes the outcome on the
same row; past `expires_at` the row becomes `expired` and the confirmation is refused. There is no
sweep: an unanswered row stays `pending-confirmation` until someone tries to confirm it (and the typed
yes/no only looks at rows still inside the window).

## 5. Privacy: what the model sees

- The **utterance** (and the voice note) go to Gemini.
- A read tool's reply goes to the user only. `AssistantCommandOutcome.Recap` is what `ast003` keeps for
  the next turn; a tool without one is kept as `[command: status; reply shown to the user, content
  withheld from you]`. Only the model's own prose (a clarification) is kept as said.
- A numbered list adds `[numbered for reference: 1=event, 2=task; …]` — kinds, never titles.
- Names are matched locally, so a title the user did not type never leaves the house.
- **The exception is `ask_notes`**: to answer a question it sends passages of up to five matching notes
  to Gemini (the same `AskNotesCommand` as the web app's "Ask your notes"), and keeps the answer in the
  recap so a follow-up can build on it. `search_notes` and `read_note` still send nothing.

## 6. Pointing by number

A list tool returns `AssistantCommandOutcome.Listed` (`ListedItem`: kind, id, label, optional
occurrence start). The pipeline stores the last one on `ast002.last_listing`. The model passes
`"ref": 2`; `ListedRefs.Pin` drops any `ref_*` field the model wrote and pins `ref_id`, `ref_kind`,
`ref_label`, `ref_at`. Tools read it with `ToolArguments.OptionalRef` / `PickTarget`, which refuse a
number not on the list or of another kind. Because the item is pinned before the call is held, a
confirmation acts on what the user saw, not on whatever is number 2 later.

## 7. Telegram path

The module never talks to the Bot API. Full sequence: [Telegram flow](../../../architecture/en/telegram-assistant-flow.md).

- **`InboundMessageReceivedHandler`** (integration-event subscriber on Channels' `InboundMessageReceived`)
  only handles messages on the `assistant` bot. Text → `InterpretCommand`. Audio → bytes through
  `IInboundMediaReader.OpenAsync(channel, bot, mediaRef)`, then `InterpretCommand` with the attachment;
  the reply starts with `🎤 "…"`. Any other file → a file queue by caption (only images/PDFs, ≤ 20 MB,
  photos named `telegram-<timestamp>.jpg`); no caption match → the reply asks where it goes. Pre-flight
  failures (disabled, no key) are only logged.
- Held calls get **Confirm / Cancel buttons** (`NotificationButton` with owner `assistant`, action
  `confirm`/`cancel`, payload = invocation id), numbered when the reply has several lines.
- **`AssistantInteractionReceivedHandler`** handles `InboundInteractionReceived` for owner `assistant`
  and sends the same Confirm/Cancel commands, replying with the outcome or "expired / already answered".
- Replies are published as Channels' **`SendAssistantReply`** inside an Assistant unit of work, so they
  ride the transactional outbox (`assistant.tars_outbox_message`) and the relay delivers them after
  commit. Channels splits replies over 4096 characters. See [Messaging](../../../architecture/en/messaging.md).

The web console runs the pipeline inline in the HTTP request; Telegram runs it in the subscriber.

**Explicit Notes capture.** Text-only `/nota <text>` is routed before interpretation, through
`INoteCapture` in Notes.Abstractions. It never calls Gemini or creates conversation/invocation rows;
an enabled AI profile/key is unnecessary. The authorized Notes command creates/reuses the capture
page, appends a dated literal paragraph and records the inbound EventId with the save. Permissions
and module flags still apply. Empty/oversized text gets usage; failures are reported without claiming
success; duplicate events get an already-captured reply. All replies use `SendAssistantReply` and the
Assistant outbox as usual. Media/captions and other text keep their original paths.

## 8. Integrations coupling

- The profile stores `chat_account_id`, an `int001_external_account` id; `SaveProfile` checks through
  `IExternalAccountReader` that it is the user's `api-key` account at the same provider.
- `GetProviders` counts the user's API keys per provider (no secret read, no external call).
- `AssistantAccountUsageSource` (`IExternalAccountUsageSource`) tells Integrations the account is used
  by `assistant`/`chat`, for the connections screen and before a disconnect.

See [Integrations — OAuth & Credentials](../../integrations/en/oauth-and-credentials.md) and
[Google OAuth and Gemini setup](../../../deployment/google-and-gemini-setup.md).

## 9. Frontend

`client-web/src/modules/assistant`:

- `pages/AssistantPage.tsx` (`/assistant`) — **Chat** tab: input, voice button, replies with
  Confirm/Cancel and the tool-call details (model, latency, tokens), "new conversation". **History**
  tab: the invocation log, each row able to reopen its conversation.
- `pages/AssistantSettingsPage.tsx` (`/settings/assistant`) — enable, pick the Gemini key from
  Integrations, model, locale, confirmation level; "test" runs the reachability probe.
- `hooks/useVoiceRecorder.ts` — `MediaRecorder` up to 150 s, re-encoded in the browser to 16 kHz mono
  WAV and posted to `/interpret/audio` (needs a secure context).
- `services/assistant.service.ts`, `hooks/useAssistant.ts`, `hooks/queryKeys.ts`, `models.ts`.

## 10. Tests

- `backend/tests/Pottmayer.Pandora.Modules.Assistant.Tests` — pipeline, confirm, profile, provider test,
  confirmation levels, both subscribers (fakes for the AI client, repositories, bus).
- Tool tests live in the owning modules (`AgendaAssistantToolsTests`, `AgendaAssistantEditToolsTests`,
  `NoteAssistantToolsTests`); Channels covers `SendAssistantReplyHandler`.
- `backend/tests/Pottmayer.Pandora.IntegrationTests/Live/AssistantLiveTests` — opt-in run against the
  real Gemini (`PANDORA_LIVE_GEMINI=1`, spends tokens; report via `PANDORA_LIVE_REPORT`).
- `client-web/src/modules/assistant/pages/AssistantPage.test.tsx`.
