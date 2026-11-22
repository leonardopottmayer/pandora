# Assistant Module — Product Plan (what is still missing)

> **Status:** the module is implemented — see [Implementation Status](implementation-status.md). This
> file keeps only what is not built yet. What exists is documented in the [Overview](overview.md) and
> [Architecture](architecture.md).
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

## 5. Persona and voice (idea, not scheduled)

The goal is an assistant that feels like a character: it has a name, a manner of speaking with a bit of
humour, and it can talk back by voice. The code keeps calling it `assistant`; only the screens and the
prompt use the name.

**Where it starts from.** Voice notes already come in through Telegram and are transcribed by Gemini.
The conversation is shared between web and Telegram, and the tools act on the other modules. Two things
are missing. First, every reply is a fixed sentence written in C# by the tool ("I couldn't find that in
your notes."), because the model only chooses the tool. Second, nothing speaks.

### Stage 1: name and personality (text)

- **Preferences** in a new Assistant table: the name (a factory default the user can change) and a
  style (plain, light humour, formal butler), with how much humour as a setting. The default stays
  discreet, because constant jokes get tiring fast.
- **Prompt:** `AssistantSystemPrompt` opens with "You are {name}…" and the style, so the model answers
  to its name.
- **Phrased replies:** a tool returns the facts (what it did, what it found), and the model writes the
  final reply in the persona's voice. This costs one more model call per message. Numbered lists stay
  built by code, so "open 2" keeps working; the persona only talks around them.
- **Screens:** the chat and the settings show the name.

### Stage 2: spoken replies (voice note in, voice note out)

- **Text to speech** over the stage 1 reply. The options:
  - **Gemini TTS ⭐**: it is the same provider and key, and the tone is steered by instructions in text.
  - **ElevenLabs**: the most natural voices, including custom ones. It is one more provider and key, and
    it is billed per character.
  - **Local (Piper or similar)**: free and private, but it sounds flatter.
- **Telegram:** the reply goes out as a voice message (`sendVoice`, OGG/Opus). Check whether the Tars
  Telegram package sends voice; if not, it needs that method.
- **Web:** a microphone button and a player under the reply.
- **Preference:** reply by voice always, only when the user spoke, or never.

### Stage 3: live conversation (only if stages 1 and 2 earn it)

- **What it is:** speaking and hearing in a continuous stream, with interruptions and replies in under a
  second.
- **How it differs:** this is a different architecture. A streaming native-audio API (for example Gemini
  Live) takes audio in and gives audio out, and it calls tools on its own.
- **What it needs:**
  - a WebSocket from the browser to the backend and on to the provider;
  - the existing `IAssistantTool`s exposed on that channel;
  - spoken confirmations.
- **Where it runs:** the web and Pandora Desktop. The Telegram bot cannot hold a voice call.

**Privacy:** each stage sends more to the provider. Stage 2 sends the reply text to the TTS service.
Stage 3 streams the user's voice the whole time a call is open. The [privacy rules](architecture.md)
need a line for each stage before it is built.

## 6. Open questions

1. **Proactive digests** ("here is your day" at 07:00). Not for now: `list_agenda` on request covers
   it, and under the privacy rule the digest would be templated in-house, not generated. A generated
   digest would need its own decision.
2. **Streaming.** Not needed for command execution; it only matters for stage 3 of
   [persona and voice](#5-persona-and-voice-idea-not-scheduled). The Tars abstraction does not expose
   it.
3. **Retention of conversations and invocations** (`ast002`–`ast004`), never purged today.
