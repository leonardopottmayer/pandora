# Assistant Module — Product Plan (what is still missing)

> **Status:** the module is implemented — see [Implementation Status](implementation-status.md). This
> file keeps only what is not built yet. What exists is documented in the [Overview](overview.md) and
> [Architecture](architecture.md); the original build order is in the [execution plan](execution-plan.md).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)

---

## 1. Second provider: OpenAI

Lands when there is a real reason beyond Gemini. The shape is ready:

- Tars: a `Pottmayer.Tars.Ai.Chat.OpenAi` client (Chat Completions with tools) registered under the
  keyed factory.
- Pandora: one more entry in `GetProvidersQueryHandler`'s catalog, its registration in
  `InfrastructureDI`, and a `CredentialProviderDescriptor(..., AuthKind.ApiKey)` in Integrations'
  `CredentialProvidersDI`.

**Done when:** a user can pick an OpenAI key and model in the settings and the same sentences work.

## 2. Eval set

An eval set of real utterances (Portuguese first), so switching model is a measured decision. The
opt-in live test (`AssistantLiveTests`, `PANDORA_LIVE_GEMINI=1`) already drives the real model through
the Telegram path but asserts nothing about its choices.

**Done when:** the set passes on the chosen model, with the numbers recorded.

## 3. Audio retention (opt-in)

Audio is discarded after transcription. Keeping it would be a per-profile opt-in, since voice is the
most sensitive thing the module touches. Not started.

## 4. Confirmation gaps

- `WhenAmbiguous` has no ambiguity signal of its own: under `balanced` it runs. Either read one (e.g.
  an argument the model inferred) or rename the policy to what it does.
- Expire held calls in the background, so the history stops showing stale `pending-confirmation`
  rows.

## 5. Open questions

1. **Proactive digests** ("here is your day" at 07:00). Not for now: `list_agenda` on request covers
   it, and under the privacy rule the digest would be templated in-house, not generated. A generated
   digest would need its own decision.
2. **Streaming.** Not needed for command execution; only for a conversational mode, if one is ever
   added. The Tars abstraction does not expose it.
3. **Retention of conversations and invocations** (`ast002`–`ast004`), never purged today.
