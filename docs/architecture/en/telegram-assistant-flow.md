# Telegram Flow — Notification and Assistant

> **Status:** Implemented (A3). Two bots, one in-process monolith.
> 🇧🇷 [Versão em português](../pt-BR/telegram-assistant-flow.md)
>
> Cross-cutting document: an end-to-end path no single module owns.
> Modules involved: [Channels](../../modules/channels/en/overview.md) ·
> [Assistant](../../modules/assistant/en/product-plan.md) ·
> [Integrations](../../modules/integrations/en/product-plan.md) · Agenda (as a *tool*).
> See also: [Messaging](messaging.md) for the outbox behind the `⇢ outbox` hops.

---

## 1. Context: two bots

Pandora talks to the user through **two Telegram bots**, resolved by name from the same
`ITelegramClientFactory` (configured under `Tars:Communication:Telegram:Bots`):

- **`notifications`** — outbound notifications + linking (`/start`) + button callbacks.
- **`assistant`** — the natural-language conversation (receives and replies).

A Telegram `chat_id` is **the same across all bots** (it is the user's id), so an account linked
through any bot is addressable by both. Only **Channels** speaks the Bot API; the other modules speak
domain only.

`⇢ outbox` marks an asynchronous hop: the event is written as a row in the producer's transaction and
a relay delivers it later (see [Messaging](messaging.md)).

---

## 2. Flow 1 — Outbound notification (system → Telegram)

**Start:** a module wants to notify the user (e.g. Agenda, a reminder is due).

```
1. Producer (e.g. Agenda)
   publishes NotifyUserRequested(userId, category, templateKey, payload)
   ⇢ outbox ⇢

2. NotifyUserRequestedHandler                          (Channels · subscriber)
   injects: IUnitOfWorkFactory, NotificationEnqueuer, IUserPreferencesReader, TimeProvider
   → resolves channels (preference ∩ usable UserChannels) + gates on quiet hours
   → per channel:

3. NotificationEnqueuer                                (Channels · service)
   injects: IUnitOfWorkFactory, INotificationTemplateRenderer, TimeProvider
   → renders the template + creates buttons (Interaction) + writes Notification (chn006) "queued"

4. NotificationDispatcherBackgroundService (hosted, every N s)
   → ISender.Send(DispatchPendingNotificationsCommand)
   → DispatchPendingNotificationsCommandHandler         (Channels · command)
     injects: IUnitOfWorkFactory, IEnumerable<IChannelTransport>, TimeProvider
     → takes a pending batch; per notification picks the IChannelTransport by Channel

5. TelegramChannelTransport                            (Channels · adapter)
   injects: ITelegramClientFactory
   → factory.GetClient("notifications").SendMessageAsync(chatId, text, keyboard)

6. TelegramBotClient (Tars) → HTTP sendMessage
```

**End:** it arrives in the user's Telegram, through the **notifications bot**.
A permanent failure (bot blocked, chat gone) → dead-letter + disable the channel; a transient one →
retry with backoff.

> There is an address-explicit variant (`SendNotificationRequested`) and the security-notification
> path (identity.\*), which call the `NotificationEnqueuer` directly, bypassing preferences.

---

## 3. Flow 2 — Conversation: message in → AI → reply

**Start:** the user sends a message to the **assistant bot** on Telegram.

```
1. TelegramLongPollingService (hosted)                 (Channels · ingress)
   injects: IServiceProvider, ITelegramClientFactory, IOptions<ChannelsOptions>, ILogger
   → one loop per bot in InboundBots; for "assistant": GetClient("assistant").GetUpdatesAsync(offset)
   → per update: triage.HandleAsync("assistant", update)

2. TelegramInboundTriage                               (Channels · ingress)
   injects: IUnitOfWorkFactory, IIntegrationEventBus, ISender, ITelegramClientFactory,
            IChannelsMetrics, TimeProvider, ILogger
   → idempotency by (bot, update_id); resolves the user by chat_id
   → free text from a linked user ⇒ publishes
     InboundMessageReceived(userId, channel="telegram", bot="assistant", text)
   ⇢ outbox ⇢
   (A /start <token> here becomes ConsumeTelegramLinkCommand — the linking path, not conversation.)

3. InboundMessageReceivedHandler                       (Assistant · subscriber)
   injects: IUnitOfWorkFactory, ISender, IIntegrationEventBus, TimeProvider, ILogger
   → filters bot=="assistant"
   → ISender.Send(InterpretCommand(userId, text))

4. InterpretCommandHandler   ⭐ the brain              (Assistant · command)
   injects: IUnitOfWorkFactory, IExternalCredentialProvider, IAiChatCompletionClientFactory,
            IUserPreferencesReader, IEnumerable<IAssistantTool>, IOptions<AssistantOptions>, TimeProvider
   → loads AssistantProfile (must be enabled)
   → Gemini key: IExternalCredentialProvider.GetApiKeyAsync(userId,"gemini")   [Integrations]
   → resolves the active conversation + last N messages (multi-turn context)
   → builds [system + history + user] + the tool catalog (from IAssistantTool)
   → clientFactory.GetClient("gemini").CompleteAsync(ChatRequest, temp 0, apiKey)

        ┌─ Gemini answers ──┐
        │  prose  → clarification (asks back)
        │  unknown tool → rejected
        │  needs confirmation (policy × level) → pending-confirmation
        │  valid tool → executes ↓
        └───────────────────┘

5. Tool, e.g. CreateReminderTool                       (Agenda · IAssistantTool)
   injects: ISender
   → parses args → ISender.Send(CreateReminderCommand) → CreateReminderCommandHandler (actually creates)
   → returns AssistantCommandOutcome(success, message)

   ↩ InterpretCommandHandler records 1 CommandInvocation + messages (user/assistant) in the conversation
     and returns InterpretResultDto(status, commandName, args, Message)

6. back in InboundMessageReceivedHandler:
   → publishes SendAssistantReply(userId, "assistant", Message)
   ⇢ outbox ⇢

7. SendAssistantReplyHandler                           (Channels · subscriber)
   injects: IUnitOfWorkFactory, ITelegramSender, ILogger
   → resolves chat_id: FindAsync(userId, Telegram).Address
   → ITelegramSender.SendAsync("assistant", chatId, text)

8. TelegramSender → factory.GetClient("assistant") → TelegramBotClient (Tars) → HTTP sendMessage
```

**End:** the reply appears in the user's Telegram, through the **assistant bot**.
The same `InterpretCommandHandler` serves the web command bar (`/assistant`) — only the inbound and
outbound channels change; the multi-turn context is identical (active, channel-agnostic conversation).

---

## 4. Module boundaries at a glance

- **Channels** is the *only* one that speaks the Bot API. It receives (ingress → event) and sends
  (event → Bot API). It never interprets meaning.
- **Assistant** speaks domain only: it consumes `InboundMessageReceived`, decides with the AI, and
  emits `SendAssistantReply`. It never touches Telegram.
- **Integrations** holds the Gemini key (fetched per call).
- **Agenda** (or another) exposes the action as an `IAssistantTool`, which internally is just a normal
  module `Command`.
- Every hop between modules is an **integration event over the outbox** — decoupled and ready to split
  into a separate service.
