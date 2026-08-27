# Status de implementação

[← Voltar ao índice](README.md)

Um retrato do que está construído versus o que está desenhado mas ainda não implementado. O roadmap
adiante fica em [product-plan.md](product-plan.md).

---

## Implementado (fases C1–C4, mais a maior parte da C5)

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
| **Frontend** | Seção de configurações de Notificações (canais, teste, preferências, histórico) em `client-web/src/modules/channels`. |

### Desvios notáveis do plano original

- **`chn005` não tem colunas de quiet hours.** Adiado — quiet hours precisam do fuso IANA do usuário,
  que o Identity ainda não carrega. Só o array ordenado `channels[]` é guardado.
- **`chn004` mantém uma PK surrogate `uuid_generate_v7()`** com índice único em
  `(provider, provider_update_id)`, em vez da PK composta que o plano propunha.
- O antigo projeto de testes `Notifications` ainda existe sob `tests/` como legado.

## Ainda não implementado (desenhado / planejado)

| Área | Status | Onde |
|---|---|---|
| **Quiet hours** | Não construído. O fuso IANA que precisam já está nas preferências do Identity, então desbloqueado. | C5 |
| **Métricas** (profundidade de fila, latência de dispatch, taxa de falha por canal, updates descartados) | Depende da fiação de OpenTelemetry no Host. | C5 |
| **Driver de webhook** | Adiado; long polling cobre o ingress. O cliente Tars já suporta `SetWebhook`. | "Talvez depois" |
| **Retry manual de uma linha morta** | Não planejado enquanto dead-letters são raras e inspecionáveis. | "Talvez depois" |
| **Categorias de notificação do Finances** | Eventos do Finances (`StatementClosed`, `ImportCompleted`, …) ainda não publicados. | Follow-up no Finances |

## Pontos em aberto conhecidos

1. **Categorias como registry tipado vs. string.** Hoje `Category` é uma string; um registry central
   daria validação no startup ao custo de mais um lugar para tocar. Tendência: string até doer.
2. **Um endereço por canal.** Dois chats do Telegram para uma pessoa é uma mudança futura deliberada (a
   constraint única bloqueia hoje).
