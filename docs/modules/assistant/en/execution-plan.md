# Assistant — Execution Plan (Gemini)

> **Status:** Execution plan. Cuts the [product-plan](product-plan.md) down to the path being built
> first: **a single hosted provider, Gemini**, with each user's key held in Integrations. Drops OpenAI
> and phase A6 (Notes/Finances/proactive) from immediate scope.
> 🇧🇷 [Versão em português](../pt-BR/execution-plan.md)

This doc is the work list. The *why* behind each decision is in the [product-plan](product-plan.md);
asynchrony without a broker is in [messaging §3](../../../architecture/en/messaging.md#3-asynchrony-without-a-broker).

> **What changed from the previous plan (local-first / Ollama):** running a local model has been
> dropped — the cost/quality tradeoff isn't worth it. The target is now **hosted Gemini**. That
> reverses the "zero coupling with Integrations" decision: the Gemini key **is** stored in
> Integrations and fetched per call. See [Locked decisions](#locked-decisions).

---

## What already exists (do not rebuild)

Two prerequisites the old plan put in phase A1 are **already done** — only the test against the real
API is missing, which is the Step 0 gate.

### Tars — the `Ai` family (uncommitted, 17 tests green)

Namespaced **by capability**, not by provider (so `Ai.Transcription.*` / `Ai.Embedding.*` can land
later without reorganizing):

| Project | Actual contents |
|---|---|
| `Pottmayer.Tars.Ai.Abstractions` | `AiException` with `IsPermanent` (permanent vs. transient), shared across all capabilities. |
| `Pottmayer.Tars.Ai.Chat.Abstractions` | `IAiChatCompletionClient.CompleteAsync(ChatRequest)`, `IAiChatCompletionClientFactory.GetClient(provider)`, and the models: `ChatRequest(Model, Messages, Tools?, Temperature?, ApiKey?)`, `ChatCompletion`, `ChatMessage`/`ChatRole`, `ToolDefinition(Name, Description, ParametersJsonSchema)`, `ToolCall(Name, Arguments, Id?)`, `TokenUsage`. |
| `Pottmayer.Tars.Ai.Chat` | `KeyedAiChatCompletionClientFactory` (resolves via keyed DI) + `AddTarsAiClientFactory`. |
| `Pottmayer.Tars.Ai.Chat.Gemini` | `GeminiAiChatCompletionClient` (`ProviderName = "gemini"`), over `v1beta/models/{model}:generateContent`, key in the `x-goog-api-key` header; options `Tars:Ai:Chat:Gemini`; wire mapper, error classifier. DI: `AddTarsAiChatGeminiOptions`, `AddTarsAiChatGeminiHttpClient`, `AddTarsAiChatCompletionClientGemini`. |

Two traits the Pandora pipeline uses directly:

- **Model and key per call.** `ChatRequest.Model` and `ChatRequest.ApiKey` arrive on each request —
  not app defaults. This is exactly the "each user brings their own key" shape. When the key comes in
  empty, it falls back to the options default. `ChatRequest.ToString` masks the key.
- **`Temperature: 0`** for the deterministic output a command pipeline wants.

Missing: transcription and embeddings have **no project yet** — they land in A4/later.

### Pandora Integrations (implemented)

- `IExternalCredentialProvider.GetApiKeyAsync(userId, "gemini")` returns the key **in plaintext**,
  decrypted from `int001_external_account` via `ISecretProtector`. `AuthKind.ApiKey`.
- `ApiKeyProviderDescriptor("gemini", "Google Gemini")` is already registered; the `SaveApiKey`
  command encrypts at rest. Adding OpenAI later is one line.

---

## Locked decisions

| Decision | Value | Reason |
|---|---|---|
| **Provider** | Gemini (hosted) | Ollama dropped. Strong tool-calling in pt-BR without keeping a GPU/homelab. Transport already implemented in Tars. |
| **Target model** | a fast Gemini (e.g. `gemini-2.5-flash`) | Low latency and good function-calling. Confirm in Step 0; it is `ChatRequest.Model`, swappable without a deploy. |
| **API key** | `int001_external_account` (`auth_kind = api_key`, provider `gemini`) | One encrypted store. `ast001.credential_ref` points there; fetched per call and passed as `ChatRequest.ApiKey`. **Coupling with Integrations is the mechanism**, not a thing to avoid. |
| **First surface** | Web (command bar) | Runs the pipeline inline; the tool call is visible immediately, no async plumbing like Telegram's. |
| **First command** | Agenda `create_reminder` | Smallest argument surface (title + timestamp); proves the end-to-end loop fast. |
| **Out of scope now** | OpenAI, A6 (Notes/Finances/proactive) | OpenAI is "one more provider descriptor" when it earns its place; A6 is a descriptor+handler per command. |

---

## Step 0 — First real Gemini call + commit `Tars.Ai`

`Tars.Ai` is green on tests but **has never talked to the real API** — which is why it isn't committed
yet. This step closes that gap; it is what remains of the old "spike" (the risk of a local 7B in
pt-BR no longer exists).

1. Get a Google AI Studio key and configure it (options `Tars:Ai:Chat:Gemini` **or** passed on
   `ChatRequest.ApiKey`).
2. A loose script in the scratchpad (C# or raw HTTP) that:
   - Defines **one** tool `create_reminder(title: string, remindAt: string ISO-8601)` as a
     `ToolDefinition`.
   - Builds the system prompt with the current local time, the IANA timezone and the week start.
   - Runs ~15–20 real Portuguese sentences: clean cases ("me lembra de ligar pro dentista amanhã às
     9"), relative dates ("sexta que vem", "daqui a 3 dias"), and ambiguous ones (which should ask
     for confirmation or fail, not invent).
   - Prints per sentence: the returned `ToolCall`, whether `remindAt` resolved correctly, and latency.
3. Confirm the target model.

**Done when:** Gemini gets tool call + timestamp right on the large majority of clean sentences, at a
tolerable latency — and `Tars.Ai` is **committed**.

---

## Step A1 — Module shell + Assistant profile

The transport already exists (above). What's left in A1 is only the Pandora side: the module scaffold,
per-user configuration, and registering Gemini in the Host.

### A1.1 — Assistant module scaffold
- Seven per-layer projects:
  `Pottmayer.Pandora.Modules.Assistant.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}`.
- PostgreSQL schema `assistant`, prefix `astXXX_`, PK `uuid_generate_v7()`.
- Module registration + DI in the Host; `assistant` connection in `docker-compose.yml` (env
  `Tars__Data__Connections__assistant__ConnectionString`).

### A1.2 — Register Gemini in the Host
- In the Host: `AddTarsAiClientFactory()` + `AddTarsAiChatGeminiOptions()` +
  `AddTarsAiChatGeminiHttpClient()` + `AddTarsAiChatCompletionClientGemini()`.
- **No** default key in the options — the key comes per call, from Integrations.

### A1.3 — `ast001_assistant_profile`
- Migration under `migrations/migrations/assistant/`. Columns (current subset):
  `user_id` (unique), `chat_provider` (`"gemini"`), `chat_model` (the Gemini model id),
  `credential_ref` (→ `int001_external_account`), `is_enabled`, `locale_override`,
  `confirmation_level`. Transcription columns and `endpoint` stay nullable/reserved.
- `AssistantProfile` aggregate in Domain; repository in Persistence.

### A1.4 — Configuration + reachability test
- `GET`/`POST /assistant/profile` (provider/model/`credential_ref`).
- `GET /assistant/providers` — lists available providers (today just `gemini`) and runs a
  **reachability test**: fetches the user's key via `GetApiKeyAsync`, builds a minimal `ChatRequest`,
  calls the `IAiChatCompletionClient`, and reports ok/error + latency. Distinguishes "no key
  configured" from "key rejected" (via `AiException.IsPermanent`).

### A1.5 — Settings frontend
- `client-web/src/modules/assistant` — a screen that saves the profile (pick a model, point at the
  Integrations Gemini account) and triggers the reachability test, showing the model's raw response.

**Done when:** the settings screen round-trips a prompt through Gemini, using the user's key stored in
Integrations, and shows the reply.

---

## Step A2 — Command pipeline (web, text)

The module's core. A Portuguese sentence → a validated tool call → an executed command →
confirmation.

### A2.1 — Command catalog
- In `Assistant.Abstractions`: `AssistantCommandDescriptor(Name, Description, ParametersJsonSchema,
  Confirmation, Examples)` and `IAssistantCommandHandler(Name, ExecuteAsync(userId, args, ct))`.
- Discovery at startup: the Assistant collects every registered descriptor and renders it as the
  `ToolDefinition[]` for the `ChatRequest`.

### A2.2 — Agenda registers `create_reminder`
- Agenda contributes **one** descriptor (`create_reminder`) from its own `Abstractions` plus a thin
  handler that maps the arguments onto the **existing** `CreateReminder` use case and sends it through
  the mediator. No duplicated business rules. (`create_task` and the rest of the catalog come later.)

### A2.3 — System prompt
- One versioned prompt in code, carrying: current local time + IANA timezone + week start (from
  Identity, via `IUserPreferencesReader`), locale, and the rule that every timestamp comes back
  **absolute and ISO-8601**. Few-shot examples come from the descriptors' `Examples` field, in
  Portuguese.

### A2.4 — Conversation and audit schema
- Migrations: `ast002_conversation` (expires after 30 minutes of silence), `ast003_message`,
  `ast004_command_invocation` (the audit trail: `command_name`, `arguments` jsonb, `status`,
  `result`/`error`, `provider`/`model`/`latency_ms`/`tokens_*` — from `TokenUsage`).

### A2.5 — `POST /assistant/interpret` (text)
- Receives `{ text }`. Inline pipeline:
  1. load the profile; fetch the key via `IExternalCredentialProvider.GetApiKeyAsync(userId, "gemini")`;
  2. build the prompt + the catalog's `ToolDefinition[]`;
  3. `IAiChatCompletionClient.CompleteAsync(new ChatRequest(model, messages, tools, Temperature: 0, ApiKey: key))`;
  4. validate the `ToolCall` (JSON schema + the owning module's validator);
  5. execute via the handler;
  6. record the invocation; respond with the interpreted intent + result.
- Failure behavior per product-plan §5 (no tool matched / missing argument / validation rejected /
  provider unreachable / malformed JSON). `AiException.IsPermanent` decides whether a retry can help.
  **Never claim success that did not happen** — status comes from the command's actual result.

### A2.6 — Confirmation
- The descriptor's `ConfirmationPolicy`, adjusted by the profile's `confirmation_level`.
  `create_reminder` is `WhenAmbiguous`: executes when unambiguous; otherwise records `ast004` as
  `pending_confirmation` (expires in 10 min) and echoes the intent back.
- `POST /assistant/invocations/{id}/confirm` and `/cancel`.

### A2.7 — Frontend
- Command bar in the client calling `/interpret`; confirmation flow (Confirm/Cancel).
- `GET /assistant/invocations` — the audit trail, to see the exact tool call the model produced.

**Done when:** typing "lembrete de pagar o aluguel dia 5 às 10h" in the browser creates the right
reminder, and the invocation log shows the exact tool call.

---

## Step A3 — Telegram input (text) *(sketch)*

Reuses [Channels C4](../../channels/en/inbound-and-linking.md), already implemented. The difference
from A2: the pipeline does **not** run inline. With hosted Gemini the reason is no longer "don't
overload the local model" — it is **latency and durability**: a multi-second network call must not
hold the inbound subscriber, and a run that dies mid-way must be a row with a state, not a lost
message.

- A subscriber on `inbound.message.#` does only the cheap part: records an invocation row and returns.
- A **background job owned by this module** drains the rows **one at a time** (the broker-free pattern
  from [§3](../../../architecture/en/messaging.md#3-asynchrony-without-a-broker)) and runs the A2
  pipeline.
- Response via `NotifyUserRequested`; confirmation buttons carry `owner_module: "assistant"`, coming
  back through `inbound.interaction.assistant.#`.

**Done when:** the same sentence typed in Telegram creates the same reminder, and *Confirm* works.

---

## Step A4 — Voice *(sketch)*

With Gemini already in place, the natural path is **Gemini's own audio input** — not a self-hosted
Whisper (that was the local-arc decision). That may make a separate `ITranscriptionClient`
unnecessary, or make it a thin capability over the same provider. Decide when we get there.

- Telegram audio read via `IInboundMediaReader` (already in Channels); the web client records with
  `MediaRecorder` and uploads multipart to `/interpret`.
- Audio retention is opt-in per profile (voice is the module's most sensitive data).

**Done when:** an audio message on Telegram creates a reminder, in Portuguese.

---

## Out of scope (for now)

- **OpenAI as a second provider.** The provider-as-port design (keyed DI in Tars +
  `ApiKeyProviderDescriptor` in Integrations) already leaves it pluggable: an `Ai.Chat.OpenAi` project
  + one registration line. It lands when there's a real reason to have it beyond Gemini.
- **A6 — Notes/Finances/proactive.** `record_transaction`, `create_note`, morning summaries. Come
  after the Agenda loop is solid — each is a new descriptor + handler, not a rewrite.

---

## The question that moved up: personal data leaves the house

In the local arc (Ollama) everything stayed on the machine. With **hosted Gemini, every utterance
leaves the house** — and read commands like `list_agenda` would send event titles to Google. This
stopped being a "settle before A5" question and now matters **immediately**, because the only provider
is hosted. Minimum acceptable for the first release:

- A clear per-profile warning that utterances go to Google.
- At the start the catalog is **write-only** (`create_reminder`), so no history leaks — reads
  (`list_agenda`, `search_items`) land together with an explicit decision about this.

---

## Execution order

```
Step 0 (real call + commit)  ──►  A1 (shell + profile)  ──►  A2 (web pipeline + create_reminder)  ──►  A3 (Telegram)  ──►  A4 (voice)
      gate                          gate                       gate                                    gate            gate
  Gemini replies, Tars.Ai       roundtrip in the UI        reminder created in the browser         same on Telegram  audio becomes a reminder
  committed                     (key via Integrations)
```

Every gate blocks progress: the previous step's "done when" must be green before moving on.
