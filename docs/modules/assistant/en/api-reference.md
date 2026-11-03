# API Reference

[← Back to index](../README.md)

Base path: **`/api/v{version}/assistant`** (the web client uses `v1.0`). Every endpoint is
authenticated and scoped to the token's user; an invocation owned by another user returns **404**
(`Assistant.InvocationNotFound`). One controller: `Presentation/Controllers/AssistantController.cs`.

---

## Profile & providers

| Method | Route | Purpose |
|---|---|---|
| GET | `/profile` | The user's profile, or the defaults when none was saved |
| POST | `/profile` | Create or replace the profile |
| GET | `/providers` | Supported providers and how many API keys the user has for each |
| POST | `/providers/{provider}/test` | Reachability probe: one tiny prompt with the chosen key (real, billed call) |

`POST /profile` body: `{ provider, chatAccountId?, model, isEnabled, localeOverride?, confirmationLevel }`.
Errors: `Assistant.ModelRequired`, `Assistant.UnknownConfirmationLevel`, `Assistant.ChatAccountNotFound`,
`Assistant.ChatAccountMismatch` (not an `api-key` account of that provider). An empty provider means
`gemini`.

`GET /providers` → `[{ provider, displayName, keyCount }]` — only `gemini` today. No secret read, no
external call.

`POST /providers/{provider}/test` body: `{ accountId?, model? }` (default model when omitted) →
`{ ok, latencyMs, reply?, error?, errorKind? }`. It always returns 200; `errorKind` is `no_key`,
`rejected` (permanent `AiException`: bad key or model) or `unreachable` (transient).

## Interpret

| Method | Route | Purpose |
|---|---|---|
| POST | `/interpret` | Interpret a typed sentence and run it inline |
| POST | `/interpret/audio` | Same, for a voice note (`multipart/form-data`) |

`POST /interpret` body: `{ text, conversationId? }`. `POST /interpret/audio` form: `audio` (file) +
optional `conversationId`; request limit is 6 MB, the pipeline refuses audio over 5 MB with a readable
clarification. Both make real, billed provider calls.

Response (`InterpretResultDto`):

```json
{
  "conversationId": "…",
  "message": "the whole reply (numbered when there are several calls)",
  "invocations": [
    { "invocationId": "…", "status": "pending-confirmation", "commandName": "delete_task",
      "arguments": "{…}", "message": "Excluir a tarefa \"…\"?" }
  ],
  "transcript": "what was heard, for audio; null for text"
}
```

Errors (before anything is recorded): `Assistant.EmptyText`, `Assistant.NotEnabled`,
`Assistant.NoApiKey`. Every other outcome is a 200 with an invocation status — see
[Data Model](data-model.md#ast004_command_invocation).

## Invocations

| Method | Route | Purpose |
|---|---|---|
| POST | `/invocations/{id}/confirm` | Run a held call with its stored arguments |
| POST | `/invocations/{id}/cancel` | Decline a held call |
| GET | `/invocations?limit=` | The audit trail, newest first (default 50, max 100) |

Confirm / cancel return `{ invocationId, status, commandName, arguments, message }`. Errors:
`Assistant.InvocationNotFound`, `Assistant.NotPendingConfirmation`, `Assistant.ConfirmationExpired`
(the row is marked `expired`). A confirmed call whose tool fails still returns 200 with status
`failed`.

`GET /invocations` → `[{ id, conversationId, utterance, commandName, arguments, status, result, error,
provider, model, latencyMs, promptTokens, completionTokens, expiresAt, createdAt }]`.

## Not HTTP

Telegram reaches the module through integration events, not this controller:
`InboundMessageReceived` (messages on the `assistant` bot) and `InboundInteractionReceived` (button
taps with owner `assistant`), both from Channels; replies go out as Channels' `SendAssistantReply`. See
[Architecture §7](architecture.md#7-telegram-path).
