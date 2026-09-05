# Fluxo Telegram — Notificação e Assistente

> **Status:** Implementado (A3). Dois bots, um só monólito in-process.
> 🇺🇸 [English version](../en/telegram-assistant-flow.md)
>
> Documento transversal: descreve um caminho ponta-a-ponta que nenhum módulo é dono sozinho.
> Módulos envolvidos: [Channels](../../modules/channels/pt-BR/overview.md) ·
> [Assistant](../../modules/assistant/pt-BR/product-plan.md) ·
> [Integrations](../../modules/integrations/pt-BR/product-plan.md) · Agenda (como *tool*).
> Ver também: [Mensageria](messaging.md) para o outbox por trás dos saltos `⇢ outbox`.

---

## 1. Contexto: dois bots

O Pandora fala com o usuário por **dois bots** de Telegram, resolvidos por nome do mesmo
`ITelegramClientFactory` (config em `Tars:Communication:Telegram:Bots`):

- **`notifications`** — saída de notificações + linking (`/start`) + callbacks de botão.
- **`assistant`** — a conversa em linguagem natural (recebe e responde).

O `chat_id` do Telegram é **o mesmo em todos os bots** (é o id do usuário), então uma conta linkada
por qualquer bot é endereçável pelos dois. Só o **Channels** fala a Bot API; os outros módulos falam
apenas domínio.

`⇢ outbox` marca um salto assíncrono: o evento é gravado numa linha na transação do produtor e um
relay o entrega depois (ver [Mensageria](messaging.md)).

---

## 2. Fluxo 1 — Notificação de saída (sistema → Telegram)

**Início:** um módulo quer avisar o usuário (ex.: Agenda, lembrete venceu).

```
1. Produtor (ex. Agenda)
   publica NotifyUserRequested(userId, category, templateKey, payload)
   ⇢ outbox ⇢

2. NotifyUserRequestedHandler                          (Channels · subscriber)
   injeta: IUnitOfWorkFactory, NotificationEnqueuer, IUserPreferencesReader, TimeProvider
   → resolve canais (preferência ∩ UserChannels utilizáveis) + corta por quiet hours
   → por canal:

3. NotificationEnqueuer                                (Channels · serviço)
   injeta: IUnitOfWorkFactory, INotificationTemplateRenderer, TimeProvider
   → renderiza o template + cria botões (Interaction) + grava Notification (chn006) "queued"

4. NotificationDispatcherBackgroundService (hosted, a cada N s)
   → ISender.Send(DispatchPendingNotificationsCommand)
   → DispatchPendingNotificationsCommandHandler         (Channels · command)
     injeta: IUnitOfWorkFactory, IEnumerable<IChannelTransport>, TimeProvider
     → pega lote pendente; por notificação escolhe o IChannelTransport pelo Channel

5. TelegramChannelTransport                            (Channels · adapter)
   injeta: ITelegramClientFactory
   → factory.GetClient("notifications").SendMessageAsync(chatId, texto, teclado)

6. TelegramBotClient (Tars) → HTTP sendMessage
```

**Fim:** chega no Telegram do usuário, pelo **bot notifications**.
Falha permanente (bot bloqueado, chat inexistente) → dead-letter + desabilita o canal; transiente →
retry com backoff.

> Há uma variante address-explícita (`SendNotificationRequested`) e o caminho de notificações de
> segurança (identity.\*), que chamam o `NotificationEnqueuer` direto sem passar pelas preferências.

---

## 3. Fluxo 2 — Conversa: entra mensagem → IA → resposta

**Início:** usuário manda mensagem pro **bot assistant** no Telegram.

```
1. TelegramLongPollingService (hosted)                 (Channels · ingress)
   injeta: IServiceProvider, ITelegramClientFactory, IOptions<ChannelsOptions>, ILogger
   → um loop por bot em InboundBots; p/ "assistant": GetClient("assistant").GetUpdatesAsync(offset)
   → por update: triage.HandleAsync("assistant", update)

2. TelegramInboundTriage                               (Channels · ingress)
   injeta: IUnitOfWorkFactory, IIntegrationEventBus, ISender, ITelegramClientFactory,
           IChannelsMetrics, TimeProvider, ILogger
   → idempotência por (bot, update_id); resolve usuário pelo chat_id
   → texto livre de usuário linkado ⇒ publica
     InboundMessageReceived(userId, channel="telegram", bot="assistant", text)
   ⇢ outbox ⇢
   (Um /start <token> aqui vira ConsumeTelegramLinkCommand — caminho de linking, não de conversa.)

3. InboundMessageReceivedHandler                       (Assistant · subscriber)
   injeta: IUnitOfWorkFactory, ISender, IIntegrationEventBus, TimeProvider, ILogger
   → filtra bot=="assistant"
   → ISender.Send(InterpretCommand(userId, text))

4. InterpretCommandHandler   ⭐ o cérebro              (Assistant · command)
   injeta: IUnitOfWorkFactory, IExternalCredentialProvider, IAiChatCompletionClientFactory,
           IUserPreferencesReader, IEnumerable<IAssistantTool>, IOptions<AssistantOptions>, TimeProvider
   → carrega AssistantProfile (exige habilitado)
   → chave Gemini: IExternalCredentialProvider.GetApiKeyAsync(userId,"gemini")   [Integrations]
   → resolve conversa ativa + últimas N msgs (contexto multi-turno)
   → monta [system + histórico + user] + catálogo de tools (dos IAssistantTool)
   → clientFactory.GetClient("gemini").CompleteAsync(ChatRequest, temp 0, apiKey)

        ┌─ Gemini responde ─┐
        │  prosa  → clarification (pergunta de volta)
        │  tool desconhecida → rejected
        │  precisa confirmar (policy × nível) → pending-confirmation
        │  tool válida → executa ↓
        └───────────────────┘

5. Tool, ex. CreateReminderTool                        (Agenda · IAssistantTool)
   injeta: ISender
   → parseia args → ISender.Send(CreateReminderCommand) → CreateReminderCommandHandler (cria de fato)
   → devolve AssistantCommandOutcome(sucesso, mensagem)

   ↩ InterpretCommandHandler grava 1 CommandInvocation + msgs (user/assistant) na conversa
     e retorna InterpretResultDto(status, commandName, args, Message)

6. de volta no InboundMessageReceivedHandler:
   → publica SendAssistantReply(userId, "assistant", Message)
   ⇢ outbox ⇢

7. SendAssistantReplyHandler                           (Channels · subscriber)
   injeta: IUnitOfWorkFactory, ITelegramSender, ILogger
   → resolve chat_id: FindAsync(userId, Telegram).Address
   → ITelegramSender.SendAsync("assistant", chatId, texto)

8. TelegramSender → factory.GetClient("assistant") → TelegramBotClient (Tars) → HTTP sendMessage
```

**Fim:** a resposta aparece no Telegram do usuário, pelo **bot assistant**.
O mesmo `InterpretCommandHandler` serve a barra de comando web (`/assistant`) — só muda o canal de
entrada e de saída; o contexto multi-turno é o mesmo (conversa ativa, agnóstica de canal).

---

## 4. Leitura rápida dos limites de módulo

- **Channels** é o *único* que fala Bot API. Recebe (ingress → evento) e envia (evento → Bot API).
  Nunca interpreta significado.
- **Assistant** fala só domínio: recebe `InboundMessageReceived`, decide com a IA, emite
  `SendAssistantReply`. Nunca toca o Telegram.
- **Integrations** guarda a chave Gemini (buscada por chamada).
- **Agenda** (ou outro) expõe a ação como `IAssistantTool`, que por dentro é só um `Command` normal do
  módulo.
- Todo salto entre módulos é **evento de integração via outbox** — desacoplado e pronto pra virar
  serviço separado.
