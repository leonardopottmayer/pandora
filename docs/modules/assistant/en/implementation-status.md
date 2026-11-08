# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built in the codebase versus what is still planned. Use it to tell "documented
because it exists" from "documented as a plan" ([Product plan](product-plan.md)).

---

## Implemented

| Area | Notes |
|---|---|
| **Frontend review** | Assistant settings and chat show query failures explicitly; failed reads do not expose editable fallback defaults. Alerts use the current Ant Design API. |
| **Module scaffold** | Seven layered projects, `assistant` schema, Host wiring, `Pandora:Assistant` options (`HistoryMessageLimit`). `Contracts` is empty. |
| **Provider** | Gemini via `Tars.Ai.Chat.Gemini`, keyed factory, model and key per call, no key in config. |
| **Profile** | `ast001`; get (with defaults) / save; chat account validated against Integrations; "used by" reported to Integrations. |
| **Reachability test** | `POST /providers/{provider}/test` with `no_key` / `rejected` / `unreachable`. |
| **Interpret pipeline** | System prompt v6 (time, zone, week start, locale, examples), multi-turn history (time-bounded by the 30-min conversation, capped at N messages), several tool calls per sentence, recap-imitation retry, every outcome recorded in `ast004`. |
| **Voice** | Transcription by the same model (audio as `ChatAttachment`), ≤ 5 MB, audio discarded; Telegram voice notes and the web recorder (WAV 16 kHz, ≤ 150 s). |
| **Confirmation** | Policies `Never`/`WhenAmbiguous`/`Always`/`Required`, levels `strict`/`balanced`/`trusting`, 10-min window, confirm/cancel endpoints, Telegram buttons, typed yes/no. |
| **Pointing by number** | `ast002.last_listing`, `ListedRefs.Pin`/`PinTarget`, targeted tools find their item before a confirmation. |
| **Privacy** | Read replies go to the user only; history keeps content-free recaps; names matched locally. |
| **Tool catalog** | 26 tools: Agenda 15 (create/update/complete/reopen/delete tasks, events, reminders; three lists), Notes 5 (`create_note`, `search_notes`, `ask_notes`, `read_note`, `append_to_note`), Finances 6 (`record_expense` into the inbox + 5 read-only). |
| **Telegram** | Subscriber on the `assistant` bot (text, voice, files), button-tap subscriber, replies via `SendAssistantReply` over the Assistant outbox + relay. |
| **Shared files** | `IAssistantFileQueue`; Finances queue by caption keywords into `fin017` (images/PDFs, ≤ 20 MB). |
| **Frontend** | `/assistant` (chat + history tabs) and `/settings/assistant`. |
| **Tests** | Module unit tests, tool tests in Agenda/Notes, the opt-in live test against real Gemini. |

### Changes from the original plan

- `ast001.credential_ref` became **`chat_account_id`** (a user may hold several Gemini keys and picks
  one); `transcription_provider/model` and `endpoint` were never created — transcription uses the chat
  model.
- `ast002` has no `source` / `is_active`: one conversation serves web and Telegram, expiry is computed
  from `last_activity_at`.
- `ast003` has no `role = tool`, `audio_ref` or `token_count`; tokens live on `ast004`.
- `ast004` has no `message_id`; statuses gained `clarification`, `provider-error` and `cancelled`, and
  carry `utterance` and `expires_at`.
- The Telegram path does not use a module-owned job draining rows: the integration-event subscriber runs
  the pipeline, and the reply is made durable by the outbox.
- No separate `ITranscriptionClient`; no conversations/messages or commands endpoints
  (`GET /conversations`, `GET /commands`) — the history tab reads `/invocations`.

## Not yet implemented

| Area | Status |
|---|---|
| **OpenAI provider** | No `Ai.Chat.OpenAi`; `GetProviders` lists only `gemini`. |
| **Eval set** | No measured utterance set for choosing a model; the live test asserts nothing about the model's choices. |
| **Audio retention opt-in** | Not built; audio is always discarded. |
| **Ambiguity detection for `WhenAmbiguous`** | Not built — see Known open points. |
| **Proactive digests** | Not planned for now. |

## Known open points

1. **`WhenAmbiguous` behaves like `Never` under `balanced`.** No confidence signal is read; only the
   level shift (`strict`) makes it confirm. No tool declares `Always`, so `balanced` holds only
   deletions.
2. **No expiry sweep.** An unanswered held call stays `pending-confirmation` past `expires_at` until a
   confirm turns it into `expired`; the history tab shows it as pending.
3. **Two lists in one turn.** `last_listing` keeps only the last one (marked `ponytail:` in
   `InterpretCommandHandler`).
4. **Conversation and message retention.** `ast002`–`ast004` are never purged.
5. The `ast004` migration comment still says `'pending_confirmation'`; the CHECK and the code use
   `pending-confirmation`.
