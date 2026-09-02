# Assistant — Implementation Plan (local-first)

> **Status:** Execution plan. Cuts the [product-plan](product-plan.md) down to the **local-first**
> path — Ollama only, self-hosted in the homelab. Drops phases A5 (OpenAI/Gemini) and A6
> (Notes/Finances/proactive) from immediate scope.
> 🇧🇷 [Versão em português](../pt-BR/local-first-plan.md)

This doc is the work list. The *why* behind each decision is in the [product-plan](product-plan.md);
asynchrony without a broker is in [messaging §3](../../../architecture/en/messaging.md#3-asynchrony-without-a-broker).

---

## Locked decisions

| Decision | Value | Reason |
|---|---|---|
| **Initial provider** | Ollama (self-hosted) | No account/API key; it is the homelab's stated target. |
| **Target model** | `qwen2.5:7b-instruct` (Q4_K_M) | Fits the 8 GB VRAM on both machines; best tool-calling in that range. Confirm in the spike. |
| **First surface** | Web (command bar) | Runs the pipeline inline; the tool call is visible immediately, no async plumbing like Telegram's. |
| **First command** | Agenda `create_reminder` | Smallest argument surface (title + timestamp); proves the end-to-end loop fast. |
| **Ollama deploy** | `service` in `docker-compose.yml`, profile `assistant` | Comes up with the stack (same cycle as Postgres/nginx); backend talks over `http://ollama:11434`. |
| **Out of local-first scope** | OpenAI/Gemini (A5), Integrations I3 | `ast001.credential_ref` is null for Ollama → **zero coupling with Integrations** in this arc. |

---

## Step 0 — Tool-calling spike *(throwaway, outside the projects)*

Validates the premise the whole module rests on: **does a local 7B emit reliable tool calls in
pt-BR?** Nothing gets scaffolded before this.

1. Install Ollama natively on the dev machine (RTX 3070) and `ollama pull qwen2.5:7b-instruct`.
2. A script (Python or loose C# in the scratchpad) that:
   - Defines **one** tool in Ollama's format: `create_reminder(title: string, remindAt: string ISO-8601)`.
   - Builds the system prompt with the current local time, the IANA timezone and the week start.
   - Runs a batch of ~15–20 real Portuguese sentences: clean cases ("me lembra de ligar pro dentista
     amanhã às 9"), relative dates ("sexta que vem", "daqui a 3 dias"), and ambiguous ones (which
     should ask for confirmation or fail, not invent).
   - Prints per sentence: the returned tool call, whether `remindAt` resolved correctly, and latency.

**Done when:** the model gets tool call + timestamp right on the large majority of clean sentences, at
a tolerable latency. If it passes, `qwen2.5:7b-instruct` becomes the fixed target. If no model in that
range passes, that changes the strategy — and it is cheap to find out now.

---

## Step A1 — `Tars.Ai` + Assistant profile

Transport building block in Tars + the module's shell + per-user configuration.

### A1.1 — `Tars.Ai.Abstractions`
- New project under `tars/src/Ai/` following the other building blocks' layout
  (`Pottmayer.Tars.Ai.Abstractions`).
- Minimal contracts for local-first: `IChatCompletionClient` (messages, tool definitions, tool
  calls, token usage), the shared message/tool model, `AiException` with permanent-vs-transient
  separation. `ITranscriptionClient` and `IEmbeddingClient` stay **reserved** (land in A4 / later),
  not implemented now.
- Doc in Tars under `docs/ai/`.

### A1.2 — `Tars.Ai.Ollama`
- `Pottmayer.Tars.Ai.Ollama`: implements `IChatCompletionClient` over `POST /api/chat` with `tools`.
- Endpoint and model configurable; no credentials.
- Maps Ollama's tool calls → the Abstractions' shared model.

### A1.3 — Assistant module scaffold
- Seven per-layer projects:
  `Pottmayer.Pandora.Modules.Assistant.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}`.
- PostgreSQL schema `assistant`, prefix `astXXX_`, PK `uuid_generate_v7()`.
- Module registration + DI in the Host; `assistant` connection in `docker-compose.yml` (env
  `Tars__Data__Connections__assistant__ConnectionString`).

### A1.4 — `ast001_assistant_profile`
- Migration under `migrations/migrations/assistant/`. Columns (local-first subset):
  `user_id` (unique), `chat_provider`, `chat_model`, `endpoint`, `is_enabled`, `locale_override`,
  `confirmation_level`. `credential_ref` and the transcription columns stay nullable/reserved.
- `AssistantProfile` aggregate in Domain; repository in Persistence.

### A1.5 — Configuration + reachability test
- `GET`/`POST /assistant/profile` (provider/model/endpoint).
- `GET /assistant/providers` — lists available providers and runs a **reachability test** (a short
  ping/prompt against the configured endpoint, reporting ok/error + latency).

### A1.6 — Settings frontend
- `client-web/src/modules/assistant` — a screen that saves the profile and triggers the reachability
  test, showing the model's raw response.

**Done when:** the settings screen does a round-trip prompt against a local Ollama and shows the
response. (Matches the A1 done-definition in the product-plan.)

---

## Step A2 — Command pipeline (web, text)

The module's core. A Portuguese sentence → a validated tool call → an executed command →
confirmation.

### A2.1 — Command catalog
- In `Assistant.Abstractions`: `AssistantCommandDescriptor(Name, Description, ParametersJsonSchema,
  Confirmation, Examples)` and `IAssistantCommandHandler(Name, ExecuteAsync(userId, args, ct))`.
- Discovery at startup: the Assistant collects every registered descriptor and renders it as the
  provider's tool definitions.

### A2.2 — Agenda registers `create_reminder`
- Agenda contributes **one** descriptor (`create_reminder`) from its own `Abstractions` plus a thin
  handler that maps the arguments onto the **existing** `CreateReminder` use case and sends it
  through the mediator. No duplicated business rules. (`create_task` and the rest of the catalog
  come later, in extended A2 / A5.)

### A2.3 — System prompt
- One versioned prompt in code, carrying: current local time + IANA timezone + week start (from
  Identity, via `IUserPreferencesReader`), locale, and the rule that every timestamp comes back
  **absolute and ISO-8601**. Few-shot examples come from the descriptors' `Examples` field, in
  Portuguese.

### A2.4 — Conversation and audit schema
- Migrations: `ast002_conversation` (expires after 30 minutes of silence), `ast003_message`,
  `ast004_command_invocation` (the audit trail: `command_name`, `arguments` jsonb, `status`,
  `result`/`error`, `provider`/`model`/`latency_ms`/`tokens_*`).

### A2.5 — `POST /assistant/interpret` (text)
- Receives `{ text }`. Inline pipeline: build prompt → `IChatCompletionClient` chat+tools → validate
  the tool call (JSON schema + the owning module's validator) → execute via the handler → record the
  invocation → respond with the interpreted intent + result.
- Failure behavior per product-plan §5 (no tool matched / missing argument / validation rejected /
  provider unreachable / malformed JSON). **Never claim success that did not happen** — status comes
  from the command's actual result.

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

Reuses [Channels C4](../../channels/en/inbound-and-linking.md), already implemented. The critical
difference from A2: the pipeline does **not** run inline — the local model takes seconds and must
not receive concurrent work.

- A subscriber on `inbound.message.#` does only the cheap part: records an invocation row and
  returns.
- A **background job owned by this module** drains the rows **one at a time** (the broker-free
  pattern from [§3](../../../architecture/en/messaging.md#3-asynchrony-without-a-broker)) and runs
  the A2 pipeline.
- Response via `NotifyUserRequested`; confirmation buttons carry `owner_module: "assistant"`, coming
  back through `inbound.interaction.assistant.#`.

**Done when:** the same sentence typed in Telegram creates the same reminder, and *Confirm* works.

---

## Step A4 — Voice *(sketch)*

- Implement `ITranscriptionClient` in Tars (a self-hosted Whisper adapter behind HTTP — the
  homelab's standard pattern).
- Telegram audio read via `IInboundMediaReader` (already in Channels); the web client records with
  `MediaRecorder` and uploads multipart to `/interpret`.
- Audio retention is opt-in per profile (voice is the module's most sensitive data).

**Done when:** an audio message on Telegram creates a reminder, in Portuguese.

---

## Out of scope (for now)

- **A5 — OpenAI/Gemini.** Depends on accounts/keys that do not exist yet, and on **Integrations I3**
  (API key management). Reopen once a key exists; the provider-as-port design already leaves this
  pluggable.
- **A6 — Notes/Finances/proactive.** `record_transaction`, `create_note`, morning summaries. Come
  after the Agenda loop is solid — each is a new descriptor + handler, not a rewrite.

---

## Execution order

```
Step 0 (spike)  ──►  A1 (Tars.Ai + profile)  ──►  A2 (web pipeline + create_reminder)  ──►  A3 (Telegram)  ──►  A4 (voice)
     gate              gate                          gate                                    gate             gate
  model passes     roundtrip in the UI          reminder created in the browser         same on Telegram   audio becomes a reminder
```

Every gate blocks progress: the previous step's "done when" must be green before moving on.
