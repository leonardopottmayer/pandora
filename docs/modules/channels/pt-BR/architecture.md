# Arquitetura

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Saída e Templates](outbound-and-templates.md), [Entrada e Vínculo](inbound-and-linking.md)

---

## 1. Organização dos projetos

Projetos por camada sob `backend/src/Modules/Channels/`:

```
Pottmayer.Pandora.Modules.Channels.
  Abstractions      → registro ChannelsModule, ChannelsOptions, IInboundMediaReader (a porta que
                      outros módulos — Assistant — chamam para ler o áudio de entrada)
  Application       → Commands, Queries, Subscribers, o enqueuer, linking, DTOs, DI
  Contracts         → eventos de integração: NotifyUserRequested, SendNotificationRequested,
                      InboundMessageReceived, InboundInteractionReceived, UserChannelDisabled
  Domain            → Aggregates, ValueObjects, Rendering, Ports (repositórios + serviços), Errors
  Infrastructure    → Transports (Email/Telegram), Ingress (long polling, triagem, mídia),
                      Jobs (dispatcher, retenção), Templates (renderer de arquivo + catálogo)
  Persistence       → EntityConfigs, Repositories, DbContext, DI
  Presentation      → ChannelsController, DI
```

## 2. A costura interna (Delivery / Ingress / Addressing)

Um módulo não significa uma pilha indiferenciada. O limite que teria dividido o módulo corre **dentro**
dele, em namespaces:

```
Application
├── Delivery   (Enqueue, DispatchPending, SetNotificationPreference, GetDeliveryHistory) — preferências · fan-out · renderização · dispatcher · retry
├── Ingress    (Subscribers, ConsumeTelegramLink, PurgeInboundUpdates) — drivers · triagem · linking · resolução de interação
└── Addressing (CreateChannelLink, UnlinkChannel, GetUserChannels, chn001) — lido pelos dois

Infrastructure
├── Transports (IChannelTransport: EmailChannelTransport · TelegramChannelTransport) — interno
├── Ingress    (TelegramLongPollingService · TelegramInboundTriage · TelegramInboundMediaReader)
└── Templates  (FileNotificationTemplateRenderer · TemplateCatalog[+Validator])
```

A regra que mantém isso honesto: nada em `Ingress` escreve na fila diretamente (publica um evento, ou
chama `Delivery` pela mesma superfície que um módulo externo usaria), e nada em `Delivery` conhece a
Bot API.

## 3. Blocos de domínio

### Agregados (`Domain/Aggregates`)

| Raiz de agregado | Responsabilidade / invariantes-chave |
|---|---|
| **Notification** | A linha durável da fila. `Pending → Sending → Sent`; `Failed`/`Dead` no esgotamento; backoff exponencial via `next_attempt_at`; conteúdo renderizado uma vez e imutável (C6). |
| **UserChannel** | Um endereço num canal. Utilizável só quando `is_verified && is_enabled`; uma falha permanente de envio o desabilita (`disabled_reason`) e publica `UserChannelDisabled`. |
| **ChannelLinkToken** | O handshake do Telegram. Uso único, com TTL; consumido por `/start <token>`. |
| **Interaction** | Um botão inline registrado + sua rota de volta. Uso único; um segundo toque é "expirado". FK para a notificação que o declarou. |
| **InboundUpdate** | Registro de idempotência + trilha de todo update recebido. Único em `(provider, provider_update_id)`; carrega a `classification` da triagem. |
| **NotificationPreference** | Lista ordenada de canais por categoria. Vazia ⇒ silenciada. `identity.*` nunca a consulta. |

### Objetos de valor (`Domain/ValueObjects`)

`Channel` (`email` \| `telegram`, com `All`), `NotificationAddress` (ciente de canal: VO de e-mail ou
chat id numérico), `NotificationContent` (`Subject`/`Body`/`IsHtml` para e-mail), `NotificationStatus`,
`InboundClassification`, `TemplateKey`. A saída estruturada do Telegram é
`Rendering/TelegramRenderedPayload` (`text`, `parseMode`, `disableNotification`,
`buttons: [{ interactionId, label }]`).

### Portas (`Domain/Ports`)

- **Serviços:** `IChannelTransport` (uma impl por canal, chamada só pelo dispatcher),
  `INotificationTemplateRenderer` (baseado em arquivo).
- **Repositórios:** um por agregado (`INotificationRepository`, `IUserChannelRepository`,
  `IChannelLinkTokenRepository`, `IInteractionRepository`, `IInboundUpdateRepository`,
  `INotificationPreferenceRepository`).

## 4. Jobs de background

Serviços hospedados do módulo (o mesmo padrão async-sem-broker do resto do Pandora — ver o
[doc de mensageria](../../../architecture/pt-BR/messaging.md)):

- **`NotificationDispatcherBackgroundService`** — pega linhas devidas (`status`, `next_attempt_at`),
  envia pelo transporte do canal, avança status/backoff.
- **`TelegramLongPollingService`** — driver de ingress `getUpdates(timeout=30)`; escreve cada update em
  `chn004` (confirmando o offset) e o triagem.
- **`InboundUpdateRetentionBackgroundService`** — diário; anula o payload `raw` de linhas `chn004` mais
  velhas que `Channels:RawRetention:RetentionDays` (padrão 7), mantendo a linha.

## 5. Decisões de design

| # | Decisão | Racional |
|---|---|---|
| **C1** | Só envio-agora; sem agendamento na fila. | Mantém o módulo sem estado quanto ao tempo de negócio; agendamento vive em quem chama. |
| **C2** | Chamadores endereçam um **usuário + intenção**; o módulo resolve canais. | Política de entrega é uma preocupação num lugar. |
| **C3** | Fan-out = N linhas com um `group_id`, dedup por `(correlation_id, channel)`. | Retry/falha independentes por canal com status honesto. |
| **C4** | `IChannelTransport` é uma porta interna, uma impl por canal. | Adiciona um canal sem `switch`; não promovida a limite de módulo (único chamador). |
| **C5** | Entrada roteada por lookup estrutural de id; o módulo nunca interpreta significado. | Evita o Channels precisar saber o que é uma tarefa. |
| **C6** | Renderiza no enfileiramento; guarda o que saiu. | Retry reenvia bytes idênticos; edições de template não reescrevem histórico. |

## 6. Bloco Tars: `Communication.Telegram`

Espelha a divisão `Communication.Email` / `.MailKit`. `Pottmayer.Tars.Communication.Telegram` é um
transporte fino sobre a Bot API (`sendMessage`, `answerCallbackQuery`, `getUpdates`, `getFile`/download,
`setWebhook`, escape MarkdownV2, validação de secret-token). Templates, retries, endereçamento, triagem
e persistência são assunto do Pandora e vivem aqui. Sua documentação está no repo do Tars
(`docs/communication/telegram.md`).

## 7. Regras transversais

- **Multi-tenant por usuário.** Toda tabela por usuário tem `user_id NOT NULL`; endpoints são escopo do
  usuário do token.
- **Um chat id nunca é aceito do cliente** — só o handshake de vínculo autoriza um.
- **`TimeProvider` em todo lugar** — TTLs, backoff e retenção são calculados contra o tempo injetado.
