# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído versus o que está desenhado mas ainda não implementado. O roadmap
adiante fica em [product-plan.md](product-plan.md).

---

## Implementado (fases C1–C5)

| Área | Notas |
|---|---|
| **Rename** | `Notifications` → `Channels`: projetos, schema `channels`, prefixo `chnXXX_`, rotas; `not001_notification` → `chn006_notification`. |
| **Canais e endereçamento** | `Channel` (`email` + `telegram`); `NotificationAddress`; `chn001_user_channel` com portões verificado/habilitado. |
| **Transporte Telegram** | `Pottmayer.Tars.Communication.Telegram` + `TelegramChannelTransport`; e-mail via `EmailChannelTransport` (MailKit). |
| **Renderização por canal** | Árvore de templates em arquivo (`FileNotificationTemplateRenderer`), `TemplateCatalog` + `TemplateCatalogValidator` falhando o startup se faltar variante. |
| **Fila durável** | `chn006` com `Pending→Sending→Sent`, backoff, `Dead`; `NotificationDispatcherBackgroundService`. |
| **Fan-out e preferências** | `chn005`; `NotifyUserRequested`; fan-out no enqueuer; dedup por `(correlation_id, channel)`; `group_id`. |
| **Subscribers do Identity** | Ativação, reset/troca de senha, MFA on/off mapeados para templates. |
| **Vínculo** | Handshake `chn002`; `POST/DELETE /{channel}/link`; `ConsumeTelegramLink` em `/start <token>`. |
| **Entrada** | `chn003`, `chn004`; `TelegramLongPollingService`; `TelegramInboundTriage`; idempotência por `(provider, provider_update_id)`; roteamento via `InboundInteractionReceived` / `InboundMessageReceived`. |
| **Interações** | Botões registrados roteados aos donos; uso único; primeiro consumidor Agenda (`task_done`, `snooze_1h`). |
| **Mídia** | `IInboundMediaReader` + `TelegramInboundMediaReader` (`getFile`/download). |
| **Tratamento de falha permanente** | Desabilita o canal + `UserChannelDisabled`. |
| **C5 — purga do bruto** | `InboundUpdateRetentionBackgroundService`; `Channels:RawRetention:{Enabled,RetentionDays}` (padrão ligado / 7 dias). |
| **C5 — histórico de entrega** | `GET /channels/notifications` (filtro + paginação); `chn006.user_id`/`category` gravados no enfileiramento; tabela de histórico em configurações. |
| **C5 — envio de teste** | `POST /channels/{channel}/test` (entregue na C2). |
| **C5 — quiet hours** | `chn007_user_notification_setting`; uma janela diária global de "não perturbe" no fuso IANA do próprio usuário (resolvido do Identity via `IUserPreferencesReader`); `suppress`/`deliver_anyway`; aplicado no `NotifyUserRequestedHandler` antes do fan-out; `GET`/`PUT /channels/notification-settings`; UI de configurações. Notificações de segurança nunca passam por esse caminho. |
| **C5 — métricas** | Meter `ChannelsMetrics` (`Pottmayer.Pandora.Modules.Channels`): `dispatched{channel,outcome}`, `dispatch.duration{channel}`, gauge `queue.depth`, `inbound.updates.discarded`. Assinado por um wildcard `AddMeter` `Pottmayer.Pandora.*` na fiação de observabilidade compartilhada, exportado via OTLP. |
| **Frontend** | Seção de configurações de Notificações (canais, teste, preferências, **quiet hours**, histórico) em `client-web/src/modules/channels`. |

### Desvios notáveis do plano original

- **Quiet hours são um ajuste global por usuário (`chn007`), não colunas na `chn005`.** O plano dizia
  que "entrariam na `chn005`", mas essa tabela é por categoria e suas linhas só existem quando o
  usuário customiza uma categoria — um único "não perturbe" teria virado uma linha por categoria.
  `chn007` é uma linha por usuário, então a janela é global; o mute por categoria continua na `chn005`.
- **`chn004` mantém uma PK surrogate `uuid_generate_v7()`** com índice único em
  `(provider, provider_update_id)`, em vez da PK composta que o plano propunha.

## Ainda não implementado (desenhado / planejado)

| Área | Status | Onde |
|---|---|---|
| **Driver de webhook** | Adiado; long polling cobre o ingress. O cliente Tars já suporta `SetWebhook`. | "Talvez depois" |
| **Retry manual de uma linha morta** | Não planejado enquanto dead-letters são raras e inspecionáveis. | "Talvez depois" |
| **Categorias de notificação do Finances** | Eventos do Finances (`StatementClosed`, `ImportCompleted`, …) ainda não publicados. | Follow-up no Finances |

## Pontos em aberto conhecidos

1. **Categorias como registry tipado vs. string.** Hoje `Category` é uma string; um registry central
   daria validação no startup ao custo de mais um lugar para tocar. Tendência: string até doer.
2. **Um endereço por canal.** Dois chats do Telegram para uma pessoa é uma mudança futura deliberada (a
   constraint única bloqueia hoje).
